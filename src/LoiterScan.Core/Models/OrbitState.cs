namespace LoiterScan.Core.Models;

/// <summary>ECI position/velocity at a point in time (km, km/s).</summary>
public readonly record struct OrbitState(
    DateTime TimeUtc,
    double X, double Y, double Z,
    double Vx, double Vy, double Vz);
