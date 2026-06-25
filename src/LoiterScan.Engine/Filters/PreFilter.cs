using LoiterScan.Core.Models;

namespace LoiterScan.Engine.Filters;

/// <summary>
/// Tier 1: reduces the candidate object set without any propagation.
/// Applies per-object exclusions then per-pair apogee/perigee gating (spec §5.3).
/// </summary>
internal static class PreFilter
{
    /// <summary>Returns objects that survive all per-object exclusion rules.</summary>
    public static IReadOnlyList<CatalogObject> FilterObjects(
        IReadOnlyList<CatalogObject> catalog, PreFilterConfig cfg, DateTime asOf)
    {
        var excludedIds       = new HashSet<long>(cfg.ExcludedIds);
        var excludedCountries = new HashSet<string>(cfg.ExcludedCountries, StringComparer.OrdinalIgnoreCase);
        var excludedGroups    = new HashSet<string>(cfg.ExcludedGroups,    StringComparer.OrdinalIgnoreCase);

        var result = new List<CatalogObject>(catalog.Count);
        foreach (var obj in catalog)
        {
            // Decayed objects have no orbit to propagate.
            if (obj.DecayDate.HasValue && obj.DecayDate.Value < asOf)            continue;

            // Elements too stale to produce reliable propagation — SGP4 drag integration
            // accumulates unbounded error over long intervals, producing negative eccentricity.
            double epochAgeDays = (asOf - obj.Elements.EpochUtc).TotalDays;
            if (epochAgeDays > cfg.MaxEpochAgeDays)                              continue;

            if (cfg.ExcludeDebris && obj.IsDebris)                               continue;
            if (excludedIds.Contains(obj.NoradId))                               continue;
            if (obj.Owner != null && excludedCountries.Contains(obj.Owner))      continue;
            if (obj.Groups.Any(g => excludedGroups.Contains(g)))                 continue;
            if (!MatchesRegime(obj.Regime, cfg.RegimeScope))                     continue;
            result.Add(obj);
        }
        return result;
    }

    /// <summary>
    /// Per-pair geometric gate: returns false if the two orbits can never be within
    /// <paramref name="thresholdKm"/> of each other (perigee of one > apogee of other + threshold).
    /// </summary>
    public static bool PairCouldMeet(CatalogObject a, CatalogObject b, double thresholdKm) =>
        a.PerigeeKm <= b.ApogeeKm + thresholdKm &&
        b.PerigeeKm <= a.ApogeeKm + thresholdKm;

    private static bool MatchesRegime(OrbitRegime regime, string scope)
    {
        // scope may be "ALL", a single value ("LEO"), or comma-separated ("LEO,MEO")
        var upper = scope.ToUpperInvariant();
        if (upper == "ALL" || string.IsNullOrWhiteSpace(upper)) return true;

        foreach (var part in upper.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool match = part switch
            {
                "LEO" => regime == OrbitRegime.Leo,
                "MEO" => regime == OrbitRegime.Meo,
                "GEO" => regime == OrbitRegime.Geo,
                "HEO" => regime == OrbitRegime.Heo,
                _     => false,
            };
            if (match) return true;
        }
        return false;
    }
}
