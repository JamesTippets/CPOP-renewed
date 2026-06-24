namespace LoiterScan.Core.Models;

/// <summary>A detected loitering event for a pair (see spec section 1.1).</summary>
public sealed record LoiteringEvent(
    PairKey Pair,
    double MinRangeKm,
    DateTime CloseApproachUtc,
    DateTime LoiterStartUtc,
    DateTime LoiterEndUtc,
    double DurationMinutes,
    double Confidence);
