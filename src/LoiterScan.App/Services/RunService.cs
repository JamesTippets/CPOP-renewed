using System.Text.Json;
using LoiterScan.Analytics;
using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

/// <summary>
/// Persists run records and loitering events; loads run history for the UI and analytics.
/// </summary>
public sealed class RunService(IDbContextFactory<LoiterScanDbContext> factory, IPropagator propagator)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task<RunEntity> StartRunAsync(RunConfig config)
    {
        await using var db = factory.CreateDbContext();
        var run = new RunEntity
        {
            StartedAt      = DateTime.UtcNow,
            Trigger        = "on-demand",
            ConfigSnapshot = JsonSerializer.Serialize(config, JsonOpts),
            Status         = "running",
        };
        db.Runs.Add(run);
        await db.SaveChangesAsync();
        return run;
    }

    public async Task CompleteRunAsync(long runId, IReadOnlyList<LoiteringEvent> events, int totalPairs)
    {
        await using var db = factory.CreateDbContext();
        var run = await db.Runs.FindAsync(runId) ?? throw new InvalidOperationException($"Run {runId} not found");

        run.Status            = "completed";
        run.TotalPairsChecked = totalPairs;
        run.EventsDetected    = events.Count;
        run.DurationSeconds   = (int)(DateTime.UtcNow - run.StartedAt).TotalSeconds;

        foreach (var ev in events)
        {
            db.LoiteringEvents.Add(new LoiteringEventEntity
            {
                RunId           = runId,
                PairKeyLow      = ev.Pair.Low,
                PairKeyHigh     = ev.Pair.High,
                NoradIdA        = ev.Pair.Low,
                NoradIdB        = ev.Pair.High,
                NameA           = ev.NameA,
                NameB           = ev.NameB,
                PairIndex       = ev.PairIndex > 0 ? ev.PairIndex : (int?)null,
                MinRangeKm      = ev.MinRangeKm,
                CaRicR          = ev.CaRicR,
                CaRicI          = ev.CaRicI,
                CaRicC          = ev.CaRicC,
                CloseApproachUtc = ev.CloseApproachUtc,
                LoiterStartUtc  = ev.LoiterStartUtc,
                LoiterEndUtc    = ev.LoiterEndUtc,
                DurationMinutes = ev.DurationMinutes,
                Confidence      = ev.Confidence,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task FailRunAsync(long runId, string reason)
    {
        await using var db = factory.CreateDbContext();
        var run = await db.Runs.FindAsync(runId);
        if (run is null) return;
        run.Status          = reason == "cancelled" ? "cancelled" : "failed";
        run.DurationSeconds = (int)(DateTime.UtcNow - run.StartedAt).TotalSeconds;
        await db.SaveChangesAsync();
    }

    public async Task<List<RunEntity>> GetRecentRunsAsync(int n = 20)
    {
        await using var db = factory.CreateDbContext();
        return await db.Runs
            .OrderByDescending(r => r.StartedAt)
            .Take(n)
            .ToListAsync();
    }

    public async Task<List<LoiteringEventEntity>> GetEventsForRunAsync(long runId)
    {
        await using var db = factory.CreateDbContext();
        var evs = await db.LoiteringEvents
            .Where(e => e.RunId == runId)
            .OrderBy(e => e.MinRangeKm)
            .ToListAsync();

        // Back-fill names for events stored before NameA/NameB were added.
        var needsName = evs.Where(e => e.NameA is null || e.NameB is null).ToList();
        if (needsName.Count > 0)
        {
            var ids = needsName.SelectMany(e => new[] { e.NoradIdA, e.NoradIdB }).Distinct().ToList();
            var nameMap = await db.CatalogObjects
                .Where(o => ids.Contains(o.NoradId))
                .Select(o => new { o.NoradId, o.Name })
                .ToDictionaryAsync(o => o.NoradId, o => o.Name);

            foreach (var ev in needsName)
            {
                if (ev.NameA is null && nameMap.TryGetValue(ev.NoradIdA, out var nA)) ev.NameA = nA;
                if (ev.NameB is null && nameMap.TryGetValue(ev.NoradIdB, out var nB)) ev.NameB = nB;
            }
        }

        // Back-fill RIC-at-close-approach for events stored before this was computed.
        var needsRic = evs.Where(e => e.CaRicR == 0 && e.CaRicI == 0 && e.CaRicC == 0).ToList();
        if (needsRic.Count > 0)
        {
            var ricIds = needsRic.SelectMany(e => new[] { e.NoradIdA, e.NoradIdB }).Distinct().ToList();
            var catMap = await db.CatalogObjects
                .Where(o => ricIds.Contains(o.NoradId))
                .ToDictionaryAsync(o => o.NoradId);

            bool anyRic = false;
            foreach (var ev in needsRic)
            {
                if (!catMap.TryGetValue(ev.NoradIdA, out var catA) ||
                    !catMap.TryGetValue(ev.NoradIdB, out var catB)) continue;

                if (!propagator.TryPropagate(catA.ToMeanElements(), ev.CloseApproachUtc, out var sA) ||
                    !propagator.TryPropagate(catB.ToMeanElements(), ev.CloseApproachUtc, out var sB)) continue;

                var (r, i, c) = RicFrame.EciToRic(sA, sB);
                ev.CaRicR = r;
                ev.CaRicI = i;
                ev.CaRicC = c;
                anyRic = true;
            }
            if (anyRic) await db.SaveChangesAsync();
        }

        return evs;
    }

    public async Task<Dictionary<long, OrbitRegime>> GetRegimeMapAsync(IEnumerable<long> noradIds)
    {
        await using var db = factory.CreateDbContext();
        var ids = noradIds.ToList();
        var rows = await db.CatalogObjects
            .Where(o => ids.Contains(o.NoradId))
            .Select(o => new { o.NoradId, o.Regime })
            .ToListAsync();
        return rows.ToDictionary(
            r => r.NoradId,
            r => Enum.TryParse<OrbitRegime>(r.Regime, out var regime) ? regime : OrbitRegime.Unknown);
    }

    public async Task<List<RunRecord>> GetAllRunRecordsAsync()
    {
        await using var db = factory.CreateDbContext();
        var runs = await db.Runs
            .Include(r => r.Events)
            .OrderBy(r => r.StartedAt)
            .ToListAsync();

        return runs.Select(r => new RunRecord(
            RunId:         r.RunId,
            StartedAt:     r.StartedAt,
            ConfigSnapshot: r.ConfigSnapshot,
            Events: r.Events.Select(e => new EventRecord(
                PairKeyLow:      e.PairKeyLow,
                PairKeyHigh:     e.PairKeyHigh,
                MinRangeKm:      e.MinRangeKm,
                CloseApproachUtc: e.CloseApproachUtc,
                DurationMinutes: e.DurationMinutes)).ToList()
        )).ToList();
    }
}
