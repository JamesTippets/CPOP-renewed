using LoiterScan.Core.Models;
using LoiterScan.Propagation.Sgp4;
using Xunit;

namespace LoiterScan.Propagation.Tests;

/// <summary>
/// Verifies Sgp4Propagator against published Vallado / Spacetrack Report #3 reference vectors
/// (tcppver.out, WGS72 constants).  SGP.NET uses WGS84 constants, which introduces a position
/// delta of ~8 m at the propagation times used here — absorbed by the 1 km tolerance.
/// MeanElements carries no ndot/nddot terms; for satellite 00005 (ndot = 2.3e-7 rev/day²)
/// the resulting approximation error is < 0.3 m at 1 440 minutes — well within tolerance.
/// </summary>
public class Sgp4PropagatorTests
{
    private readonly Sgp4Propagator _propagator = new();

    /// <summary>
    /// Position tolerance applied to all reference-vector comparisons (km).
    /// Chosen to accommodate the WGS72 vs WGS84 gravity-constant difference (~8 m)
    /// and the ndot = 0 approximation (< 1 m for the selected test case).
    /// </summary>
    private const double PosTolKm = 1.0;

    /// <summary>Velocity tolerance (km/s).</summary>
    private const double VelTolKms = 1e-3;

    // ── Satellite 00005 (Vanguard 1 debris) ──────────────────────────────────
    //
    // TLE from Vallado SGP4-VER.TLE:
    //   1 00005U 58002B   00179.78495062  .00000023  00000-0  28098-4 0  4753
    //   2 00005  34.2682 348.7242 1859667 331.7664  19.3264 10.82419157413667
    //
    // Reference vectors from tcppver.out (Vallado C++ reference distribution, WGS72):
    //   t (min)       X (km)          Y (km)          Z (km)
    //                 Vx (km/s)       Vy (km/s)       Vz (km/s)

    [Theory]
    [MemberData(nameof(Sat00005ReferenceVectors))]
    public void Propagate_Sat00005_MatchesReferenceVectors(
        double tMinutes,
        double refX, double refY, double refZ,
        double refVx, double refVy, double refVz)
    {
        var elements = Sat00005();
        var atUtc    = elements.EpochUtc.AddMinutes(tMinutes);

        var state = _propagator.Propagate(elements, atUtc);

        Assert.InRange(state.X,  refX  - PosTolKm,  refX  + PosTolKm);
        Assert.InRange(state.Y,  refY  - PosTolKm,  refY  + PosTolKm);
        Assert.InRange(state.Z,  refZ  - PosTolKm,  refZ  + PosTolKm);
        Assert.InRange(state.Vx, refVx - VelTolKms, refVx + VelTolKms);
        Assert.InRange(state.Vy, refVy - VelTolKms, refVy + VelTolKms);
        Assert.InRange(state.Vz, refVz - VelTolKms, refVz + VelTolKms);
    }

    public static IEnumerable<object[]> Sat00005ReferenceVectors()
    {
        // t (min)    X              Y               Z             Vx           Vy            Vz
        yield return [ 0.0,     7022.46529266, -1400.08296755,     0.03995155,  1.893841015,  6.405893759,  4.534807250 ];
        yield return [ 360.0,  -7154.03120202, -3783.17682504, -3536.19412294,  4.741887409, -4.151817765, -2.093935425 ];
        yield return [ 720.0,  -7134.59340119,  6531.68641334,  3260.27186483, -4.113793027, -2.911922039, -2.557327851 ];
        yield return [ 1080.0,  5568.53901181,  4492.06992591,  3863.87641983, -4.209106476,  5.159719888,  2.744852980 ];
        yield return [ 1440.0,  -938.55923943, -6268.18748831, -4294.02924751,  7.536105209, -0.427127707,  0.989878080 ];
    }

    // ── Sanity check — orbit radius ───────────────────────────────────────────

    [Fact]
    public void Propagate_OrbitRadius_IsWithinExpectedRange()
    {
        // For satellite 00005: perigee ~600 km, apogee ~3900 km altitude
        // Expected radius range: 6378 + 600 to 6378 + 3900 = 6978 to 10278 km
        var elements = Sat00005();

        for (int tMin = 0; tMin <= 1440; tMin += 60)
        {
            var state = _propagator.Propagate(elements, elements.EpochUtc.AddMinutes(tMin));
            double r = Math.Sqrt(state.X * state.X + state.Y * state.Y + state.Z * state.Z);
            Assert.InRange(r, 6978.0, 10278.0);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MeanElements Sat00005() => new(
        MeanMotionRevPerDay: 10.82419157,
        Eccentricity:        0.1859667,
        InclinationDeg:      34.2682,
        RaanDeg:             348.7242,
        ArgPerigeeDeg:       331.7664,
        MeanAnomalyDeg:      19.3264,
        BStar:               2.8098e-5,   // TLE "28098-4" = 0.28098 × 10⁻⁴
        EpochUtc:            TleEpoch(0, 179.78495062));

    /// <summary>
    /// Converts a TLE epoch (2-digit year + fractional day-of-year) to a UTC DateTime.
    /// Follows the standard rule: 2-digit years 00–56 map to 2000–2056,
    /// years 57–99 map to 1957–1999.
    /// Day-of-year is 1-based (1 = Jan 1 00:00:00 UTC).
    /// </summary>
    private static DateTime TleEpoch(int year2, double dayOfYear)
    {
        int fullYear = year2 < 57 ? 2000 + year2 : 1900 + year2;
        return new DateTime(fullYear, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                   .AddDays(dayOfYear - 1.0);
    }
}
