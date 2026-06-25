using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Engine.Internal;

namespace LoiterScan.Engine.Filters;

/// <summary>
/// Tier 4: 1-min / 5-km loitering detection with bridged-excursion logic (spec §1.1, §5.6).
///
/// A qualifying event is a span starting when range first drops ≤ 5 km.
/// Excursions above 5 km lasting ≤ 5 consecutive minutes are bridged:
///   the timer is NOT reset and those minutes count toward the 1-hour total.
/// The span ends (and is evaluated) when an excursion exceeds 5 minutes or the window ends.
/// An event fires when the span duration (measured from span_start to last_in_range inclusive)
/// is ≥ minDurationMinutes.
/// </summary>
internal static class LoiteringDetector
{
    public static IReadOnlyList<LoiteringEvent> Run(
        IReadOnlyList<CandidatePair> candidates,
        IPropagator propagator,
        int stepMinutes,
        double thresholdKm,
        int minDurationMinutes,
        int maxExcursionMinutes,
        IProgress<PipelineProgress>? progress,
        CancellationToken ct)
    {
        var step   = TimeSpan.FromMinutes(stepMinutes);
        var events = new List<LoiteringEvent>();

        for (int ci = 0; ci < candidates.Count; ci++)
        {
            if (ct.IsCancellationRequested) break;
            var candidate = candidates[ci];

            progress?.Report(new PipelineProgress("Detection", ci + 1, candidates.Count, events.Count,
                $"Detection: pair {ci + 1:N0} / {candidates.Count:N0}  ({events.Count:N0} events)"));

            foreach (var window in candidate.Windows)
                events.AddRange(
                    DetectInWindow(candidate, propagator, window, step,
                                   thresholdKm, minDurationMinutes, maxExcursionMinutes));
        }

        return events;
    }

    private static IEnumerable<LoiteringEvent> DetectInWindow(
        CandidatePair pair,
        IPropagator propagator,
        TimeWindow window,
        TimeSpan step,
        double thresholdKm,
        int minDurationMinutes,
        int maxExcursionMinutes)
    {
        // State machine
        DateTime? spanStart    = null;
        DateTime? lastInRange  = null;
        int       excursionLen = 0;
        double    minRange     = double.MaxValue;
        DateTime? closeApproachT = null;

        var t = window.Start;
        while (t <= window.End)
        {
            if (!propagator.TryPropagate(pair.A.Elements, t, out var sa) ||
                !propagator.TryPropagate(pair.B.Elements, t, out var sb))
            {
                t += step;
                continue;
            }
            double dist = Math.Sqrt(
                (sa.X - sb.X) * (sa.X - sb.X) +
                (sa.Y - sb.Y) * (sa.Y - sb.Y) +
                (sa.Z - sb.Z) * (sa.Z - sb.Z));

            if (dist <= thresholdKm)
            {
                if (spanStart == null) spanStart = t;
                lastInRange  = t;
                excursionLen = 0;
                if (dist < minRange) { minRange = dist; closeApproachT = t; }
            }
            else if (spanStart != null)
            {
                excursionLen++;
                if (excursionLen > maxExcursionMinutes)
                {
                    // Excursion too long — emit if qualifying
                    if (lastInRange.HasValue)
                    {
                        double durMin = (lastInRange.Value - spanStart.Value).TotalMinutes + step.TotalMinutes;
                        if (durMin >= minDurationMinutes)
                            yield return BuildEvent(pair, spanStart.Value, lastInRange.Value,
                                                    durMin, minRange, closeApproachT!.Value);
                    }
                    // Reset span
                    spanStart = null; lastInRange = null; excursionLen = 0;
                    minRange = double.MaxValue; closeApproachT = null;
                }
            }

            t += step;
        }

        // End of window — check open span
        if (spanStart.HasValue && lastInRange.HasValue)
        {
            double durMin = (lastInRange.Value - spanStart.Value).TotalMinutes + step.TotalMinutes;
            if (durMin >= minDurationMinutes)
                yield return BuildEvent(pair, spanStart.Value, lastInRange.Value,
                                        durMin, minRange, closeApproachT!.Value);
        }
    }

    private static LoiteringEvent BuildEvent(
        CandidatePair pair, DateTime start, DateTime end,
        double durationMinutes, double minRangeKm, DateTime closeApproach)
    {
        // Confidence proxy: mean epoch age (days); fresher = higher confidence
        double epochAgeMean = (pair.A.EpochAgeDays + pair.B.EpochAgeDays) / 2;
        double confidence   = Math.Max(0, 1 - epochAgeMean / 30.0);

        return new LoiteringEvent(
            Pair:             new PairKey(pair.A.NoradId, pair.B.NoradId),
            MinRangeKm:       minRangeKm,
            CloseApproachUtc: closeApproach,
            LoiterStartUtc:   start,
            LoiterEndUtc:     end,
            DurationMinutes:  durationMinutes,
            Confidence:       confidence);
    }
}
