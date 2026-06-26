using System.Diagnostics;
using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Engine.Filters;

namespace LoiterScan.Engine;

/// <summary>Per-phase progress for the UI.</summary>
/// <param name="Phase">Short phase name ("PreFilter", "Coarse", "Fine", "Detection", …).</param>
/// <param name="Processed">Items/steps completed so far in this phase.</param>
/// <param name="Total">Total items/steps in this phase (0 = unknown → indeterminate).</param>
/// <param name="Candidates">Objects or pairs that have survived to this point.</param>
/// <param name="Message">Human-readable status line for the UI.</param>
public sealed record PipelineProgress(
    string Phase,
    int    Processed,
    int    Total,
    int    Candidates,
    string Message = "");

/// <summary>
/// Returned by <see cref="DetectionPipeline.RunAsync"/>. Contains the detected events and
/// the number of candidate pairs the coarse filter identified (used to populate TotalPairsChecked).
/// </summary>
public sealed record PipelineResult(
    IReadOnlyList<LoiteringEvent> Events,
    int CoarsePairsChecked);

/// <summary>
/// Orchestrates the four-tier cascade: pre-filter → coarse (15 min / 50 km) →
/// fine (5 min / 25 km, windowed ±30 min) → loitering detection (1 min / 5 km,
/// contiguous ≥ 1 h with ≤ 5-min bridged excursions). Runs off the UI thread with
/// progress reporting and cancellation. Depends only on the Core interfaces (spec §5).
/// </summary>
public sealed class DetectionPipeline
{
    private readonly IPropagator    _propagator;
    private readonly ICatalogSource _catalog;

    public DetectionPipeline(IPropagator propagator, ICatalogSource catalog)
    {
        _propagator = propagator;
        _catalog    = catalog;
    }

    /// <param name="config">Run configuration (cascade params + exclusions).</param>
    /// <param name="t0">Propagation epoch (default: UtcNow at call time).</param>
    public async Task<PipelineResult> RunAsync(
        RunConfig config,
        DateTime? t0 = null,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        var origin    = t0 ?? DateTime.UtcNow;
        var cascade   = config.Cascade;
        var preFilter = config.PreFilter;
        var sw        = Stopwatch.StartNew();

        // ── 0. Fetch catalog ─────────────────────────────────────────────────
        progress?.Report(new PipelineProgress("Fetch", 0, 0, 0, "Fetching catalog…"));
        var catalog = await _catalog.FetchCatalogAsync(ct);
        progress?.Report(new PipelineProgress("Fetch", 1, 1, catalog.Count,
            $"Fetched {catalog.Count:N0} objects ({sw.Elapsed.TotalSeconds:F1}s)"));

        // ── 1. Pre-filter ────────────────────────────────────────────────────
        sw.Restart();
        progress?.Report(new PipelineProgress("PreFilter", 0, catalog.Count, 0,
            $"Pre-filter: 0 / {catalog.Count:N0} objects…"));
        var survivors = PreFilter.FilterObjects(catalog, preFilter, origin);
        progress?.Report(new PipelineProgress("PreFilter", catalog.Count, catalog.Count, survivors.Count,
            $"Pre-filter complete: {survivors.Count:N0} / {catalog.Count:N0} objects survive ({sw.Elapsed.TotalSeconds:F1}s)"));

        // ── 1b. Propagation pre-validation ───────────────────────────────────
        // Try one propagation per survivor at the run epoch. Objects that fail
        // (decayed, negative eccentricity, degenerate elements) are silently
        // discarded — one check here avoids repeated failures every timestep.
        sw.Restart();
        progress?.Report(new PipelineProgress("Validate", 0, survivors.Count, 0,
            $"Validating: 0 / {survivors.Count:N0} objects…"));
        var validated = new List<CatalogObject>(survivors.Count);
        for (int i = 0; i < survivors.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (_propagator.TryPropagate(survivors[i].Elements, origin, out _))
                validated.Add(survivors[i]);
            if (i % 250 == 0 || i == survivors.Count - 1)
                progress?.Report(new PipelineProgress("Validate", i + 1, survivors.Count, validated.Count,
                    $"Validating: {i + 1:N0} / {survivors.Count:N0}  ({validated.Count:N0} valid)"));
        }
        progress?.Report(new PipelineProgress("Validate", validated.Count, validated.Count, validated.Count,
            $"Validation complete: {validated.Count:N0} of {survivors.Count:N0} objects propagable ({sw.Elapsed.TotalSeconds:F1}s)"));

        // ── 2. Coarse filter (spatial index) ─────────────────────────────────
        sw.Restart();
        var coarseCandidates = await Task.Run(() =>
            CoarseFilter.Run(
                validated, _propagator, origin,
                cascade.HorizonDays,
                cascade.Coarse.StepMinutes,
                cascade.Coarse.ThresholdKm,
                cascade.Buffers.CoarseToFineMinutes,
                progress, ct), ct);

        // Throw on the continuation thread so the debugger sees the catch block.
        // (The filters exit their loops via IsCancellationRequested rather than throwing,
        //  which avoids first-chance breaks on the thread-pool thread.)
        ct.ThrowIfCancellationRequested();
        var coarseElapsed = sw.Elapsed;

        // Apply geometric gating, then drop explicitly excluded pairs
        // (co-orbital / docked objects that always trigger false positives).
        var excludedPairs = new HashSet<PairKey>(config.PreFilter.ExcludedPairs);
        var gated = coarseCandidates
            .Where(p => PreFilter.PairCouldMeet(p.A, p.B, cascade.Coarse.ThresholdKm))
            .Where(p => !excludedPairs.Contains(new PairKey(p.A.NoradId, p.B.NoradId)))
            .Where(p => !PreFilter.PairSharesExcludedGroup(p.A, p.B, preFilter))
            .ToList();

        progress?.Report(new PipelineProgress("Coarse", gated.Count, gated.Count, gated.Count,
            $"Coarse complete: {gated.Count:N0} candidate pairs after gating ({coarseElapsed.TotalSeconds:F1}s)"));

        // ── 3. Fine filter ───────────────────────────────────────────────────
        sw.Restart();
        var fineCandidates = await Task.Run(() =>
            FineFilter.Run(
                gated, _propagator,
                cascade.Fine.StepMinutes,
                cascade.Fine.ThresholdKm,
                cascade.Buffers.FineToDetectionMinutes,
                progress, ct), ct);

        ct.ThrowIfCancellationRequested();

        progress?.Report(new PipelineProgress("Fine", fineCandidates.Count, fineCandidates.Count, fineCandidates.Count,
            $"Fine filter complete: {fineCandidates.Count:N0} pairs remain ({sw.Elapsed.TotalSeconds:F1}s)"));

        // ── 4. Loitering detection ───────────────────────────────────────────
        sw.Restart();
        var events = await Task.Run(() =>
            LoiteringDetector.Run(
                fineCandidates, _propagator,
                cascade.Detection.StepMinutes,
                cascade.Detection.ThresholdKm,
                cascade.Loiter.MinDurationMinutes,
                cascade.Loiter.ExcursionAllowanceMinutes,
                progress, ct), ct);

        ct.ThrowIfCancellationRequested();

        // Stamp each event with its pair's 1-based position in the gated candidate list.
        var pairIndexMap = gated
            .Select((p, i) => (Key: new PairKey(p.A.NoradId, p.B.NoradId), Index: i + 1))
            .ToDictionary(x => x.Key, x => x.Index);
        var indexedEvents = events
            .Select(e => e with { PairIndex = pairIndexMap.TryGetValue(e.Pair, out var idx) ? idx : 0 })
            .ToList();

        progress?.Report(new PipelineProgress("Detection", indexedEvents.Count, indexedEvents.Count, indexedEvents.Count,
            $"Detection complete: {indexedEvents.Count:N0} loitering event(s) ({sw.Elapsed.TotalSeconds:F1}s)"));

        return new PipelineResult(indexedEvents, coarseCandidates.Count);
    }
}
