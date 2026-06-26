using LoiterScan.Core.Models;

namespace LoiterScan.Analytics;

/// <summary>
/// Groups loitering events by canonical pair key across runs.
///
/// Episode keying: events whose predicted close-approach times are within 12 hours of each other
/// are counted as the same episode (reconfirmation across overlapping 7-day windows).
/// Events separated by more than 12 hours are counted as distinct episodes.
///
/// Trend: the direction of min-range change from the earliest to the latest distinct episode.
/// </summary>
public sealed class RecurringPairsAnalyzer
{
    private static readonly TimeSpan EpisodeWindow = TimeSpan.FromHours(12);

    /// <param name="runs">All runs to aggregate, in any order.</param>
    /// <returns>One summary per observed pair, ordered by recurrence count descending.</returns>
    public IReadOnlyList<RecurringPairSummary> Analyze(IReadOnlyList<RunRecord> runs)
    {
        var allSightings =
            (from run in runs
             from ev  in run.Events
             select (Run: run, Ev: ev, Key: new PairKey(ev.PairKeyLow, ev.PairKeyHigh)))
            .ToList();

        var results = new List<RecurringPairSummary>();

        foreach (var group in allSightings.GroupBy(s => s.Key))
        {
            var sightings = group.ToList();

            int      recurrenceCount  = sightings.Select(s => s.Run.RunId).Distinct().Count();
            DateTime firstSeen        = sightings.Min(s => s.Run.StartedAt);
            DateTime lastSeen         = sightings.Max(s => s.Run.StartedAt);
            double   allTimeMinRange  = sightings.Min(s => s.Ev.MinRangeKm);

            var episodes = GroupIntoEpisodes(sightings);
            var trend    = ComputeTrend(episodes);

            results.Add(new RecurringPairSummary(
                Pair:              group.Key,
                RecurrenceCount:   recurrenceCount,
                EpisodeCount:      episodes.Count,
                FirstSeen:         firstSeen,
                LastSeen:          lastSeen,
                AllTimeMinRangeKm: allTimeMinRange,
                Trend:             trend));
        }

        return results.OrderByDescending(r => r.RecurrenceCount).ToList();
    }

    // Returns (minRange, earliestRunStarted) per episode, sorted by close-approach time.
    private static List<(double MinRange, DateTime EarliestRun)> GroupIntoEpisodes(
        List<(RunRecord Run, EventRecord Ev, PairKey Key)> sightings)
    {
        var byApproach = sightings.OrderBy(s => s.Ev.CloseApproachUtc).ToList();
        var episodes   = new List<(double MinRange, DateTime EarliestRun)>();

        if (byApproach.Count == 0) return episodes;

        DateTime bucketStart      = byApproach[0].Ev.CloseApproachUtc;
        double   bucketMin        = byApproach[0].Ev.MinRangeKm;
        DateTime bucketEarliestRun = byApproach[0].Run.StartedAt;

        for (int i = 1; i < byApproach.Count; i++)
        {
            var s = byApproach[i];
            if (s.Ev.CloseApproachUtc - bucketStart <= EpisodeWindow)
            {
                if (s.Ev.MinRangeKm < bucketMin)      bucketMin         = s.Ev.MinRangeKm;
                if (s.Run.StartedAt  < bucketEarliestRun) bucketEarliestRun = s.Run.StartedAt;
            }
            else
            {
                episodes.Add((bucketMin, bucketEarliestRun));
                bucketStart        = s.Ev.CloseApproachUtc;
                bucketMin          = s.Ev.MinRangeKm;
                bucketEarliestRun  = s.Run.StartedAt;
            }
        }
        episodes.Add((bucketMin, bucketEarliestRun));
        return episodes;
    }

    private static RangeTrend ComputeTrend(List<(double MinRange, DateTime EarliestRun)> episodes)
    {
        if (episodes.Count < 2) return RangeTrend.Stable;

        var ordered = episodes.OrderBy(e => e.EarliestRun).ToList();
        double first = ordered[0].MinRange;
        double last  = ordered[^1].MinRange;

        if (last < first - 0.01) return RangeTrend.Closing;
        if (last > first + 0.01) return RangeTrend.Opening;
        return RangeTrend.Stable;
    }
}
