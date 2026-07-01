namespace LoiterScan.Core.Models;

/// <summary>A detected loitering event for a pair (see spec section 1.1).</summary>
public sealed record LoiteringEvent(
    PairKey Pair,
    double MinRangeKm,
    DateTime CloseApproachUtc,
    DateTime LoiterStartUtc,
    DateTime LoiterEndUtc,
    double DurationMinutes,
    double Confidence,
    double CaRicR = 0,
    double CaRicI = 0,
    double CaRicC = 0,
    string? NameA = null,
    string? NameB = null,
    int PairIndex = 0);
