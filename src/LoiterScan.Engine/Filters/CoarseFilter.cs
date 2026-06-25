using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Engine.Internal;
using LoiterScan.Engine.Spatial;

namespace LoiterScan.Engine.Filters;

/// <summary>
/// Tier 2: propagates every surviving object at 15-min steps over the full horizon,
/// uses a 3D spatial grid to find pairs within 50 km at any step, and emits
/// candidate time windows (spec §5.4).  Windows are expanded by ±bufferMin before return.
/// </summary>
internal static class CoarseFilter
{
    public static IReadOnlyList<CandidatePair> Run(
        IReadOnlyList<CatalogObject> objects,
        IPropagator propagator,
        DateTime t0,
        int horizonDays,
        int stepMinutes,
        double thresholdKm,
        int bufferMinutes,
        IProgress<PipelineProgress>? progress,
        CancellationToken ct)
    {
        var step   = TimeSpan.FromMinutes(stepMinutes);
        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        int steps  = horizonDays * 24 * 60 / stepMinutes;

        // flaggedTimes[(a.NoradId, b.NoradId)] → sorted set of step times where pair was within threshold
        var flagged = new Dictionary<PairKey, SortedSet<DateTime>>();
        // Build index mapping NoradId → CatalogObject for pair reconstruction
        var objByIdx = objects;

        var positions = new (double X, double Y, double Z)[objects.Count];
        var grid = new SpatialGrid(thresholdKm);

        for (int s = 0; s <= steps; s++)
        {
            ct.ThrowIfCancellationRequested();
            var t = t0 + step * s;

            // Propagate all objects at this step; skip objects whose elements are invalid
            for (int i = 0; i < objects.Count; i++)
            {
                if (propagator.TryPropagate(objects[i].Elements, t, out var st))
                    positions[i] = (st.X, st.Y, st.Z);
                else
                    positions[i] = (double.NaN, double.NaN, double.NaN);
            }

            // Build spatial grid — skip objects whose propagation failed
            grid.Clear();
            for (int i = 0; i < objects.Count; i++)
            {
                if (double.IsNaN(positions[i].X)) continue;
                grid.Add(i, positions[i].X, positions[i].Y, positions[i].Z);
            }

            // Find pairs within threshold
            for (int i = 0; i < objects.Count; i++)
            {
                if (double.IsNaN(positions[i].X)) continue;
                var (xi, yi, zi) = positions[i];
                foreach (int j in grid.QueryNeighbours(xi, yi, zi, thresholdKm))
                {
                    if (j <= i) continue;  // canonical ordering, no duplicates
                    var (xj, yj, zj) = positions[j];
                    double dist = Math.Sqrt(
                        (xi - xj) * (xi - xj) +
                        (yi - yj) * (yi - yj) +
                        (zi - zj) * (zi - zj));
                    if (dist > thresholdKm) continue;

                    var key = new PairKey(objects[i].NoradId, objects[j].NoradId);
                    if (!flagged.TryGetValue(key, out var times))
                        flagged[key] = times = [];
                    times.Add(t);
                }
            }

            if (s % 100 == 0)
                progress?.Report(new PipelineProgress("Coarse", s, flagged.Count));
        }

        // Build an object lookup for pair reconstruction
        var byNorad = objects.ToDictionary(o => o.NoradId);
        var result  = new List<CandidatePair>(flagged.Count);

        foreach (var (key, times) in flagged)
        {
            var windows = MergeIntoWindows(times, step, buffer);
            result.Add(new CandidatePair(byNorad[key.Low], byNorad[key.High], windows));
        }

        progress?.Report(new PipelineProgress("Coarse", steps, result.Count));
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
            if (first)
            {
                blockStart = blockEnd = t;
                first = false;
            }
            else if (t - blockEnd <= step)
            {
                blockEnd = t;
            }
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
}
