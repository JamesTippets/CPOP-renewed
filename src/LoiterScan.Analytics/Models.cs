using LoiterScan.Core.Models;

namespace LoiterScan.Analytics;

// ── Analyzer inputs ───────────────────────────────────────────────────────────

/// <summary>What the analyzers need from a persisted run row.</summary>
public sealed record RunRecord(
    long RunId,
    DateTime StartedAt,
    string ConfigSnapshot,
    IReadOnlyList<EventRecord> Events);

/// <summary>What the analyzers need from a loitering_events row.</summary>
public sealed record EventRecord(
    long PairKeyLow,
    long PairKeyHigh,
    double MinRangeKm,
    DateTime CloseApproachUtc,
    double DurationMinutes);

// ── RecurringPairsAnalyzer output ─────────────────────────────────────────────

/// <summary>Direction of min-range change across distinct predicted episodes.</summary>
public enum RangeTrend { Stable, Closing, Opening }

/// <summary>Aggregated summary for a pair seen in one or more runs.</summary>
public sealed record RecurringPairSummary(
    PairKey Pair,
    /// <summary>Number of distinct runs in which this pair was detected.</summary>
    int RecurrenceCount,
    /// <summary>
    /// Number of distinct predicted close-approach episodes
    /// (events within 12 h of each other are the same episode).
    /// </summary>
    int EpisodeCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    double AllTimeMinRangeKm,
    RangeTrend Trend);

// ── TrendAnalyzer output ──────────────────────────────────────────────────────

/// <summary>Per-run event counts within one config-scoped group.</summary>
public sealed record RunTrendPoint(
    long RunId,
    DateTime StartedAt,
    string ConfigSnapshot,
    int TotalEvents,
    int NewPairs,
    int RecurringPairs);

/// <summary>Trend data for all runs that share the same ConfigSnapshot.</summary>
public sealed record ConfigScopedTrend(
    string ConfigSnapshot,
    IReadOnlyList<RunTrendPoint> Points);
