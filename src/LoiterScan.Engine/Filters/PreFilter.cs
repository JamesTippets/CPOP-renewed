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
        IReadOnlyList<CatalogObject> catalog, PreFilterConfig cfg)
    {
        var excludedIds       = new HashSet<long>(cfg.ExcludedIds);
        var excludedCountries = new HashSet<string>(cfg.ExcludedCountries, StringComparer.OrdinalIgnoreCase);
        var excludedGroups    = new HashSet<string>(cfg.ExcludedGroups,    StringComparer.OrdinalIgnoreCase);

        var result = new List<CatalogObject>(catalog.Count);
        foreach (var obj in catalog)
        {
            if (cfg.ExcludeDebris && obj.IsDebris)                                          continue;
            if (excludedIds.Contains(obj.NoradId))                                           continue;
            if (obj.Owner    != null && excludedCountries.Contains(obj.Owner))               continue;
            if (obj.Groups.Any(g => excludedGroups.Contains(g)))                             continue;
            if (!MatchesRegime(obj.Regime, cfg.RegimeScope))                                 continue;
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

    private static bool MatchesRegime(OrbitRegime regime, string scope) =>
        scope.ToUpperInvariant() switch
        {
            "LEO" => regime == OrbitRegime.Leo,
            "MEO" => regime == OrbitRegime.Meo,
            "GEO" => regime == OrbitRegime.Geo,
            "HEO" => regime == OrbitRegime.Heo,
            _     => true  // "ALL" or unrecognised
        };
}
