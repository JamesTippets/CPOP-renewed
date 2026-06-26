using LoiterScan.Acquisition.CelesTrak;
using LoiterScan.Core.Models;
using Xunit;

namespace LoiterScan.Acquisition.Tests;

/// <summary>
/// Tests for CelesTrakCatalogSource parsing and SATCAT join using local fixture files.
/// No live HTTP — BuildCatalog is called directly.
/// </summary>
public class CelesTrakCatalogSourceTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string OmmJson    => File.ReadAllText(FixturePath("sample-omm.json"));
    private static string SatcatJson => File.ReadAllText(FixturePath("sample-satcat.json"));

    [Fact]
    public void BuildCatalog_ReturnsOneObjectPerNoradId()
    {
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        // 4 unique NORAD IDs in sample-omm.json (20055 has no SATCAT entry)
        Assert.Equal(4, catalog.Count);
    }

    [Fact]
    public void BuildCatalog_Iss_HasCorrectElements()
    {
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var iss = catalog.Single(o => o.NoradId == 25544);

        Assert.Equal("ISS (ZARYA)", iss.Name);
        Assert.Equal("1998-067A",   iss.IntlDesignator);
        Assert.Equal(15.49812301,   iss.Elements.MeanMotionRevPerDay, 6);
        Assert.Equal(0.0001766,     iss.Elements.Eccentricity, 7);
        Assert.Equal(51.6407,       iss.Elements.InclinationDeg, 4);
    }

    [Fact]
    public void BuildCatalog_Iss_SatcatJoined()
    {
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var iss = catalog.Single(o => o.NoradId == 25544);

        Assert.Equal("ISS",     iss.Owner);
        Assert.Equal("PAYLOAD", iss.ObjectType);
        Assert.False(iss.IsDebris);
        Assert.Null(iss.DecayDate);
    }

    [Fact]
    public void BuildCatalog_Iss_DerivedFieldsCorrect()
    {
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var iss = catalog.Single(o => o.NoradId == 25544);

        // ISS is in LEO
        Assert.Equal(OrbitRegime.Leo, iss.Regime);

        // Apogee radius should be > 6771 km (400 km altitude + R_Earth 6371)
        // and < 8371 km (2000 km altitude + R_Earth)
        Assert.InRange(iss.ApogeeKm, 6700, 8371);
        Assert.InRange(iss.PerigeeKm, 6700, 8371);
        Assert.True(iss.ApogeeKm >= iss.PerigeeKm);

        // Epoch age should be positive (fixture epoch 2026-01-15 is in the past relative to today)
        Assert.True(iss.EpochAgeDays >= 0);
    }

    [Fact]
    public void BuildCatalog_Debris_FlaggedCorrectly()
    {
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var deb = catalog.Single(o => o.NoradId == 49445);

        Assert.True(deb.IsDebris);
        Assert.Equal("CIS",    deb.Owner);
        Assert.Equal("DEBRIS", deb.ObjectType);
    }

    [Fact]
    public void BuildCatalog_MissingSatcat_DoesNotThrow()
    {
        // NORAD 20055 (SL-16 R/B) is in OMM but not in sample-satcat.json
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var rb = catalog.Single(o => o.NoradId == 20055);

        Assert.NotNull(rb);
        Assert.Null(rb.Owner);      // no SATCAT entry
        Assert.Null(rb.ObjectType);
        Assert.False(rb.IsDebris);  // default false when objectType is null
    }

    [Fact]
    public void BuildCatalog_HighlyEccentric_ClassifiedHeo()
    {
        // SL-16 R/B has eccentricity 0.738 → perigee alt ~380 km, apogee alt ~40 000 km → HEO
        var catalog = CelesTrakCatalogSource.BuildCatalog(OmmJson, SatcatJson);
        var rb = catalog.Single(o => o.NoradId == 20055);

        Assert.Equal(OrbitRegime.Heo, rb.Regime);
        Assert.True(rb.ApogeeKm > rb.PerigeeKm + 10_000, "HEO object should have large apogee-perigee spread");
    }

    // ── Unit tests for the mapper helpers (accessible via InternalsVisibleTo) ──

    [Theory]
    [InlineData(15.49812301, 0.0001766,  OrbitRegime.Leo)]  // ISS ~420 km
    [InlineData(15.06,       0.000120,   OrbitRegime.Leo)]  // Starlink ~550 km
    [InlineData(15.82,       0.005000,   OrbitRegime.Leo)]  // Debris ~380-450 km
    [InlineData(2.00568,     0.738000,   OrbitRegime.Heo)]  // Molniya-like
    public void ClassifyRegime_KnownCases(double mm, double ecc, OrbitRegime expected)
    {
        var (apo, peri) = CatalogObjectMapper.DeriveApogeePerigee(mm, ecc);
        var regime = CatalogObjectMapper.ClassifyRegime(apo, peri, ecc);
        Assert.Equal(expected, regime);
    }

    [Fact]
    public void DeriveApogeePerigee_CircularLeo_ReasonableAltitude()
    {
        // ISS: n ≈ 15.498 rev/day, e ≈ 0.00018 → a ≈ 6793 km
        var (apo, peri) = CatalogObjectMapper.DeriveApogeePerigee(15.49812301, 0.0001766);
        double apoAlt  = apo  - 6371;
        double periAlt = peri - 6371;

        Assert.InRange(apoAlt,  350, 500);  // roughly ISS altitude
        Assert.InRange(periAlt, 350, 500);
    }
}
