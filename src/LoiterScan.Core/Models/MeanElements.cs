namespace LoiterScan.Core.Models;

/// <summary>Mean orbital elements consumed directly by the propagator (never a TLE string,
/// so the OMM / 9-digit catalog path is preserved).</summary>
public sealed record MeanElements(
    double MeanMotionRevPerDay,
    double Eccentricity,
    double InclinationDeg,
    double RaanDeg,
    double ArgPerigeeDeg,
    double MeanAnomalyDeg,
    double BStar,
    DateTime EpochUtc);
