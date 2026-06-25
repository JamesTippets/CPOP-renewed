using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Engine.Filters;

namespace LoiterScan.Engine;

/// <summary>Per-phase progress for the UI (Phase, items processed, candidates surviving).</summary>
public sealed record PipelineProgress(string Phase, int Processed, int Candidates);

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
    public async Task<IReadOnlyList<LoiteringEvent>> RunAsync(
        RunConfig config,
        DateTime? t0 = null,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        var origin = t0 ?? DateTime.UtcNow;
        var cascade = config.Cascade;
        var preFilter = config.PreFilter;

        // ── 0. Fetch catalog ─────────────────────────────────────────────────
        progress?.Report(new PipelineProgress("Fetch", 0, 0));
        var catalog = await _catalog.FetchCatalogAsync(ct);

        // ── 1. Pre-filter ────────────────────────────────────────────────────
        progress?.Report(new PipelineProgress("PreFilter", 0, catalog.Count));
        var survivors = PreFilter.FilterObjects(catalog, preFilter);

        // Apply apogee/perigee gating to reduce pair set before coarse.
        // We build a pre-gated catalog: objects that could meet ANY other surviving object.
        // (Gating is per-pair inside CoarseFilter; here we just pass filtered objects.)
        progress?.Report(new PipelineProgress("PreFilter", survivors.Count, survivors.Count));

        // ── 2. Coarse filter (spatial index) ─────────────────────────────────
        var coarseCandidates = await Task.Run(() =>
            CoarseFilter.Run(
                survivors, _propagator, origin,
                cascade.HorizonDays,
                cascade.Coarse.StepMinutes,
                cascade.Coarse.ThresholdKm,
                cascade.Buffers.CoarseToFineMinutes,
                progress, ct), ct);

        // Apply geometric gating to discard impossible pairs that the coarse filter
        // raised (can happen when gating wasn't applied per-pair before propagation).
        var gated = coarseCandidates
            .Where(p => PreFilter.PairCouldMeet(p.A, p.B, cascade.Coarse.ThresholdKm))
            .ToList();

        progress?.Report(new PipelineProgress("Coarse", gated.Count, gated.Count));

        // ── 3. Fine filter ───────────────────────────────────────────────────
        var fineCandidates = await Task.Run(() =>
            FineFilter.Run(
                gated, _propagator,
                cascade.Fine.StepMinutes,
                cascade.Fine.ThresholdKm,
                cascade.Buffers.FineToDetectionMinutes,
                ct), ct);

        progress?.Report(new PipelineProgress("Fine", fineCandidates.Count, fineCandidates.Count));

        // ── 4. Loitering detection ───────────────────────────────────────────
        var events = await Task.Run(() =>
            LoiteringDetector.Run(
                fineCandidates, _propagator,
                cascade.Detection.StepMinutes,
                cascade.Detection.ThresholdKm,
                cascade.Loiter.MinDurationMinutes,
                cascade.Loiter.ExcursionAllowanceMinutes,
                ct), ct);

        progress?.Report(new PipelineProgress("Detection", events.Count, events.Count));
        return events;
    }
}
