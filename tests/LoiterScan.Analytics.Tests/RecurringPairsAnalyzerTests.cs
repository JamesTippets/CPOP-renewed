using LoiterScan.Analytics;
using LoiterScan.Core.Models;
using Xunit;

namespace LoiterScan.Analytics.Tests;

public class RecurringPairsAnalyzerTests
{
    private static readonly DateTime T0 =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RunRecord MakeRun(long runId, DateTime startedAt, string config,
        params EventRecord[] events) =>
        new(runId, startedAt, config, events);

    // PairKeyLow/High stored in canonical order (low ≤ high) — mirrors how EF persists them.
    private static EventRecord MakeEvent(long a, long b, double minRange,
        DateTime closeApproach, double duration = 90) =>
        new(Math.Min(a, b), Math.Max(a, b), minRange, closeApproach, duration);

    // ── RecurringPairsAnalyzer ────────────────────────────────────────────────

    [Fact]
    public void RecurringPairs_CountsRecurrenceAcrossRuns()
    {
        // Pair (1,2) reconfirmed in 3 runs; close approaches all within 2 h → 1 episode.
        var approach = T0.AddDays(2);
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(1, 2, 3.0, approach)),
            MakeRun(2, T0.AddDays(1), "cfg-A", MakeEvent(1, 2, 2.8, approach.AddHours(1))),
            MakeRun(3, T0.AddDays(2), "cfg-A", MakeEvent(1, 2, 2.5, approach.AddHours(2))),
        };

        var summaries = new RecurringPairsAnalyzer().Analyze(runs);

        var s = summaries.Single(x => x.Pair == new PairKey(1, 2));
        Assert.Equal(3, s.RecurrenceCount);
        Assert.Equal(1, s.EpisodeCount);            // same predicted event → 1 episode
        Assert.Equal(2.5, s.AllTimeMinRangeKm);     // best of the 3 sightings
        Assert.Equal(T0,            s.FirstSeen);
        Assert.Equal(T0.AddDays(2), s.LastSeen);
    }

    [Fact]
    public void RecurringPairs_DistinctEpisodes_CountedSeparately()
    {
        // Pair (3,4) detected 7 days apart — genuinely different predicted close approaches.
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(3, 4, 3.0, T0.AddDays(1))),
            MakeRun(2, T0.AddDays(7), "cfg-A", MakeEvent(3, 4, 2.5, T0.AddDays(8))),
        };

        var summaries = new RecurringPairsAnalyzer().Analyze(runs);

        var s = summaries.Single(x => x.Pair == new PairKey(3, 4));
        Assert.Equal(2, s.RecurrenceCount);
        Assert.Equal(2, s.EpisodeCount);    // two distinct predicted events
    }

    [Fact]
    public void RecurringPairs_Trend_Closing_WhenMinRangeDecreases()
    {
        // Two distinct episodes; min range shrinks from 4.0 → 2.0.
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(5, 6, 4.0, T0.AddDays(1))),
            MakeRun(2, T0.AddDays(7), "cfg-A", MakeEvent(5, 6, 2.0, T0.AddDays(8))),
        };

        var s = new RecurringPairsAnalyzer().Analyze(runs).Single();
        Assert.Equal(RangeTrend.Closing, s.Trend);
    }

    [Fact]
    public void RecurringPairs_Trend_Opening_WhenMinRangeIncreases()
    {
        // Two distinct episodes; min range grows from 1.5 → 3.5.
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(7, 8, 1.5, T0.AddDays(1))),
            MakeRun(2, T0.AddDays(7), "cfg-A", MakeEvent(7, 8, 3.5, T0.AddDays(8))),
        };

        var s = new RecurringPairsAnalyzer().Analyze(runs).Single();
        Assert.Equal(RangeTrend.Opening, s.Trend);
    }

    [Fact]
    public void RecurringPairs_Trend_Stable_ForSingleEpisode()
    {
        // Three runs, all reconfirming one episode — no multi-episode trend possible.
        var approach = T0.AddDays(3);
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(9, 10, 2.0, approach)),
            MakeRun(2, T0.AddDays(1), "cfg-A", MakeEvent(9, 10, 1.9, approach.AddHours(3))),
        };

        var s = new RecurringPairsAnalyzer().Analyze(runs).Single();
        Assert.Equal(1, s.EpisodeCount);
        Assert.Equal(RangeTrend.Stable, s.Trend);
    }

    [Fact]
    public void RecurringPairs_AllTimeMin_IsLowestAcrossAllSightings()
    {
        // The min-range in sighting 3 (1.2 km) beats all others.
        var approach = T0.AddDays(4);
        var runs = new[]
        {
            MakeRun(1, T0,            "cfg-A", MakeEvent(11, 12, 3.5, approach)),
            MakeRun(2, T0.AddDays(1), "cfg-A", MakeEvent(11, 12, 2.1, approach.AddHours(2))),
            MakeRun(3, T0.AddDays(2), "cfg-A", MakeEvent(11, 12, 1.2, approach.AddHours(4))),
        };

        var s = new RecurringPairsAnalyzer().Analyze(runs).Single();
        Assert.Equal(1.2, s.AllTimeMinRangeKm);
    }

    // ── TrendAnalyzer ─────────────────────────────────────────────────────────

    [Fact]
    public void TrendAnalyzer_GroupsByConfigSnapshot()
    {
        // Config A has 2 runs; Config B has 1 run — they must be in separate groups.
        var cfgA = """{"coarseThreshold":50}""";
        var cfgB = """{"coarseThreshold":40}""";

        var runs = new[]
        {
            MakeRun(1, T0,            cfgA, MakeEvent(1, 2, 3.0, T0.AddDays(1))),
            MakeRun(2, T0.AddDays(7), cfgA, MakeEvent(1, 2, 2.8, T0.AddDays(8))),
            MakeRun(3, T0.AddDays(1), cfgB, MakeEvent(1, 2, 4.0, T0.AddDays(2))),
        };

        var report = new TrendAnalyzer().Analyze(runs);

        Assert.Equal(2, report.Count);
        Assert.Equal(2, report.Single(g => g.ConfigSnapshot == cfgA).Points.Count);
        Assert.Single(report.Single(g => g.ConfigSnapshot == cfgB).Points);
    }

    [Fact]
    public void TrendAnalyzer_ClassifiesNewVsRecurringPairs()
    {
        // Run 1: pair (1,2) — new.
        // Run 2: pair (1,2) recurring + pair (3,4) new.
        // Run 3: pair (1,2) recurring + pair (3,4) recurring + pair (5,6) new.
        var cfg      = """{"coarseThreshold":50}""";
        var approach = T0.AddDays(3);

        var runs = new[]
        {
            MakeRun(1, T0,             cfg,
                MakeEvent(1, 2, 3.0, approach)),
            MakeRun(2, T0.AddDays(7),  cfg,
                MakeEvent(1, 2, 2.8, approach.AddDays(7)),
                MakeEvent(3, 4, 4.0, approach.AddDays(7))),
            MakeRun(3, T0.AddDays(14), cfg,
                MakeEvent(1, 2, 2.5, approach.AddDays(14)),
                MakeEvent(3, 4, 3.5, approach.AddDays(14)),
                MakeEvent(5, 6, 1.0, approach.AddDays(14))),
        };

        var points = new TrendAnalyzer().Analyze(runs)
            .Single().Points
            .OrderBy(p => p.StartedAt).ToList();

        Assert.Equal(1, points[0].TotalEvents);
        Assert.Equal(1, points[0].NewPairs);       Assert.Equal(0, points[0].RecurringPairs);

        Assert.Equal(2, points[1].TotalEvents);
        Assert.Equal(1, points[1].NewPairs);       Assert.Equal(1, points[1].RecurringPairs);

        Assert.Equal(3, points[2].TotalEvents);
        Assert.Equal(1, points[2].NewPairs);       Assert.Equal(2, points[2].RecurringPairs);
    }

    [Fact]
    public void TrendAnalyzer_DifferentConfigs_NotConfoundedTogether()
    {
        // Pair (1,2) seen in config A then config B.
        // In config B it must be "new" — prior sighting in config A doesn't count.
        var cfgA = """{"t":50}""";
        var cfgB = """{"t":40}""";

        var runs = new[]
        {
            MakeRun(1, T0,            cfgA, MakeEvent(1, 2, 3.0, T0.AddDays(1))),
            MakeRun(2, T0.AddDays(7), cfgB, MakeEvent(1, 2, 3.0, T0.AddDays(8))),
        };

        var report = new TrendAnalyzer().Analyze(runs);
        var groupB = report.Single(g => g.ConfigSnapshot == cfgB).Points.Single();

        Assert.Equal(1, groupB.NewPairs);       // new in this config scope
        Assert.Equal(0, groupB.RecurringPairs); // not seen before in config B
    }

    [Fact]
    public void TrendAnalyzer_MultipleEventsForSamePair_CountedAsOnePair()
    {
        // A run may detect the same pair in multiple windows → distinct events but one pair key.
        // new/recurring counts are per pair, not per event.
        var cfg = """{"t":50}""";

        var approach = T0.AddDays(2);
        var runs = new[]
        {
            MakeRun(1, T0, cfg,
                MakeEvent(1, 2, 3.0, approach),
                MakeEvent(1, 2, 2.5, approach.AddHours(30))),   // same pair, second window
        };

        var points = new TrendAnalyzer().Analyze(runs).Single().Points;

        Assert.Equal(2, points.Single().TotalEvents);   // 2 events
        Assert.Equal(1, points.Single().NewPairs);      // but 1 unique pair
        Assert.Equal(0, points.Single().RecurringPairs);
    }
}
