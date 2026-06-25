using System.Globalization;
using LoiterScan.Acquisition.CelesTrak.Dto;
using LoiterScan.Core.Models;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>
/// Converts an OMM record (optionally joined with a SATCAT record) into a <see cref="CatalogObject"/>.
/// Derived fields — apogee/perigee radii, orbital regime, epoch age — are computed here at ingest
/// so the detection engine can read precomputed columns without re-deriving them per pair.
/// </summary>
internal static class CatalogObjectMapper
{
    // Earth standard gravitational parameter (m³/s²)
    private const double Mu = 3.986004418e14;
    // Mean Earth radius (km)
    private const double REarth = 6371.0;

    public static CatalogObject Map(OmmRecord omm, SatcatRecord? satcat, DateTime? asOf = null)
    {
        var epoch = ParseEpoch(omm.Epoch);

        // --- Mean elements ---
        var elements = new MeanElements(
            MeanMotionRevPerDay: omm.MeanMotion,
            Eccentricity:        omm.Eccentricity,
            InclinationDeg:      omm.Inclination,
            RaanDeg:             omm.RaOfAscNode,
            ArgPerigeeDeg:       omm.ArgOfPericenter,
            MeanAnomalyDeg:      omm.MeanAnomaly,
            BStar:               omm.Bstar,
            EpochUtc:            epoch);

        // --- Derived orbital geometry ---
        var (apogeeKm, perigeeKm) = DeriveApogeePerigee(omm.MeanMotion, omm.Eccentricity);
        var regime = ClassifyRegime(apogeeKm, perigeeKm, omm.Eccentricity);

        var now = asOf ?? DateTime.UtcNow;
        double epochAgeDays = (now - epoch).TotalDays;

        // --- SATCAT fields (graceful on missing join) ---
        string? owner      = satcat?.Country;
        string? objectType = satcat?.ObjectType;
        bool    isDebris   = objectType != null &&
                             objectType.Contains("DEBRIS", StringComparison.OrdinalIgnoreCase);
        DateTime? decayDate = ParseDecay(satcat?.Decay);

        return new CatalogObject(
            NoradId:        omm.NoradCatId,
            Name:           omm.ObjectName ?? satcat?.SatName,
            IntlDesignator: omm.IntlDesignator,
            Elements:       elements,
            Owner:          owner,
            ObjectType:     objectType,
            IsDebris:       isDebris,
            DecayDate:      decayDate,
            ApogeeKm:       apogeeKm,
            PerigeeKm:      perigeeKm,
            Regime:         regime,
            EpochAgeDays:   epochAgeDays);
    }

    // ---------- helpers ----------

    internal static (double ApogeeKm, double PerigeeKm) DeriveApogeePerigee(
        double meanMotionRevPerDay, double eccentricity)
    {
        // Kepler's third law: a = (μ / n²)^(1/3), n in rad/s
        double nRadS = meanMotionRevPerDay * 2 * Math.PI / 86400.0;
        double aKm = Math.Pow(Mu / (nRadS * nRadS), 1.0 / 3.0) / 1000.0;
        return (aKm * (1 + eccentricity), aKm * (1 - eccentricity));
    }

    internal static OrbitRegime ClassifyRegime(double apogeeKm, double perigeeKm, double eccentricity)
    {
        double apogeeAlt  = apogeeKm  - REarth;
        double perigeeAlt = perigeeKm - REarth;

        if (apogeeAlt <= 2000)
            return OrbitRegime.Leo;

        // GEO: apogee and perigee both near 35 786 km altitude, nearly circular
        if (Math.Abs(apogeeAlt - 35_786) <= 200 && perigeeAlt > 35_400 && eccentricity < 0.01)
            return OrbitRegime.Geo;

        // HEO: perigee in LEO range, apogee much higher (Molniya-like)
        if (perigeeAlt < 2000 && apogeeAlt > 2000)
            return OrbitRegime.Heo;

        if (perigeeAlt > 2000)
            return OrbitRegime.Meo;

        return OrbitRegime.Unknown;
    }

    private static DateTime ParseEpoch(string epoch)
    {
        // OMM epochs are typically ISO 8601: "2024-01-15T02:57:46.963200"
        // Some sources may omit sub-seconds or use other precision.
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.ffffff",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
        };
        return DateTime.ParseExact(epoch, formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static DateTime? ParseDecay(string? decay)
    {
        if (string.IsNullOrWhiteSpace(decay)) return null;
        return DateTime.TryParse(decay, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d)
            ? d : null;
    }
}
