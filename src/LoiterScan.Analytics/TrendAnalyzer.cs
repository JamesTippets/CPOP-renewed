using LoiterScan.Core.Models;

namespace LoiterScan.Analytics;

/// <summary>
/// Cross-run trend analysis, config-scoped so threshold changes don't confound the data.
///
/// Runs are grouped by their exact ConfigSnapshot string. Within each group, runs are ordered
/// by StartedAt and each pair is classified as "new" (first time seen in this config scope)
/// or "recurring" (already observed in a prior run of the same config).
/// </summary>
public sealed class TrendAnalyzer
{
    /// <param name="runs">All runs to analyze, in any order.</param>
    /// <returns>One ConfigScopedTrend per distinct config snapshot, ordered by earliest run.</returns>
    public IReadOnlyList<ConfigScopedTrend> Analyze(IReadOnlyList<RunRecord> runs)
    {
        var result = new List<ConfigScopedTrend>();

        foreach (var configGroup in runs.GroupBy(r => r.ConfigSnapshot))
        {
            var orderedRuns = configGroup.OrderBy(r => r.StartedAt).ToList();
            var seenPairs   = new HashSet<PairKey>();
            var points      = new List<RunTrendPoint>(orderedRuns.Count);

            foreach (var run in orderedRuns)
            {
                var runPairs = run.Events
                    .Select(e => new PairKey(e.PairKeyLow, e.PairKeyHigh))
                    .Distinct()
                    .ToList();

                int newPairs       = runPairs.Count(p => !seenPairs.Contains(p));
                int recurringPairs = runPairs.Count(p =>  seenPairs.Contains(p));

                foreach (var p in runPairs) seenPairs.Add(p);

                points.Add(new RunTrendPoint(
                    RunId:          run.RunId,
                    StartedAt:      run.StartedAt,
                    ConfigSnapshot: run.ConfigSnapshot,
                    TotalEvents:    run.Events.Count,
                    NewPairs:       newPairs,
                    RecurringPairs: recurringPairs));
            }

            result.Add(new ConfigScopedTrend(configGroup.Key, points));
        }

        return result.OrderBy(g => g.Points.Min(p => p.StartedAt)).ToList();
    }
}
