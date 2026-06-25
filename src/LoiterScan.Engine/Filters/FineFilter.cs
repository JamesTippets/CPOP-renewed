using LoiterScan.Core.Abstractions;
using LoiterScan.Engine.Internal;

namespace LoiterScan.Engine.Filters;

/// <summary>
/// Tier 3: re-propagates each coarse-surviving pair at 5-min steps, restricted to the
/// coarse candidate windows (which already include the ±30-min buffer).
/// Returns pairs whose refined windows survive the 25-km threshold (spec §5.5).
/// Surviving windows are expanded by ±<paramref name="bufferMinutes"/> for the next tier.
/// </summary>
internal static class FineFilter
{
    public static IReadOnlyList<CandidatePair> Run(
        IReadOnlyList<CandidatePair> candidates,
        IPropagator propagator,
        int stepMinutes,
        double thresholdKm,
        int bufferMinutes,
        IProgress<PipelineProgress>? progress,
        CancellationToken ct)
    {
        var step   = TimeSpan.FromMinutes(stepMinutes);
        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        var result = new List<CandidatePair>(candidates.Count);

        for (int pi = 0; pi < candidates.Count; pi++)
        {
            if (ct.IsCancellationRequested) break;
            var candidate = candidates[pi];

            progress?.Report(new PipelineProgress("Fine", pi + 1, candidates.Count, result.Count,
                $"Fine filter: pair {pi + 1:N0} / {candidates.Count:N0}  ({result.Count:N0} surviving)"));

            var refinedWindows = new List<TimeWindow>();

            foreach (var window in candidate.Windows)
            {
                // Collect times within this window where pair is within threshold
                var flaggedTimes = new SortedSet<DateTime>();
                var t = window.Start;
                while (t <= window.End)
                {
                    if (propagator.TryPropagate(candidate.A.Elements, t, out var sa) &&
                        propagator.TryPropagate(candidate.B.Elements, t, out var sb))
                    {
                        double dist = Distance(sa.X, sa.Y, sa.Z, sb.X, sb.Y, sb.Z);
                        if (dist <= thresholdKm)
                            flaggedTimes.Add(t);
                    }
                    t += step;
                }

                if (flaggedTimes.Count > 0)
                    refinedWindows.AddRange(MergeIntoWindows(flaggedTimes, step, buffer));
            }

            if (refinedWindows.Count > 0)
                result.Add(candidate with { Windows = refinedWindows });
        }

        return result;
    }

    private static IReadOnlyList<TimeWindow> MergeIntoWindows(
        SortedSet<DateTime> times, TimeSpan step, TimeSpan buffer)
    {
        var windows = new List<TimeWindow>();
        DateTime blockStart = default, blockEnd = default;
        bool first = true;

        foreach (var t in times)
        {
            if (first)                     { blockStart = blockEnd = t; first = false; }
            else if (t - blockEnd <= step) { blockEnd = t; }
            else
            {
                windows.Add(new TimeWindow(blockStart - buffer, blockEnd + buffer));
                blockStart = blockEnd = t;
            }
        }
        if (!first)
            windows.Add(new TimeWindow(blockStart - buffer, blockEnd + buffer));

        return windows;
    }

    private static double Distance(double x1, double y1, double z1,
                                   double x2, double y2, double z2) =>
        Math.Sqrt((x1 - x2) * (x1 - x2) +
                  (y1 - y2) * (y1 - y2) +
                  (z1 - z2) * (z1 - z2));
}
