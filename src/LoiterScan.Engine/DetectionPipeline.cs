using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;

namespace LoiterScan.Engine;

/// <summary>Per-phase progress for the UI (Phase, items processed, candidates surviving).</summary>
public sealed record PipelineProgress(string Phase, int Processed, int Candidates);

/// <summary>
/// Orchestrates the cascade: pre-filter -> coarse (15m/50km) -> fine (5m/25km, windowed +/-30m)
/// -> loitering detection (1m/5km, contiguous >=1h with a 5m excursion allowance). Runs off the
/// UI thread with progress reporting and cancellation. Depends only on the Core interfaces.
/// </summary>
public sealed class DetectionPipeline
{
    private readonly IPropagator _propagator;
    private readonly ICatalogSource _catalog;

    public DetectionPipeline(IPropagator propagator, ICatalogSource catalog)
    {
        _propagator = propagator;
        _catalog = catalog;
    }

    public Task<IReadOnlyList<LoiteringEvent>> RunAsync(
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
        => throw new NotImplementedException("TODO: implement the four-tier cascade.");
}
