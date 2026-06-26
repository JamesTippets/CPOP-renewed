using System.Text.Json;
using LoiterScan.Analytics;
using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

/// <summary>
/// Persists run records and loitering events; loads run history for the UI and analytics.
/// </summary>
public sealed class RunService(IDbContextFactory<LoiterScanDbContext> factory)
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
                MinRangeKm      = ev.MinRangeKm,
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
        return await db.LoiteringEvents
            .Where(e => e.RunId == runId)
            .OrderBy(e => e.MinRangeKm)
            .ToListAsync();
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
