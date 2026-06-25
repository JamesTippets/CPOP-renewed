using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>
/// v1 catalog source: fetches GP data as OMM JSON from CelesTrak, joins SATCAT metadata,
/// computes derived fields on ingest, and returns a deduplicated <see cref="CatalogObject"/> list.
/// Takes an <see cref="HttpClient"/> so the caller (composition root or test) controls base address,
/// timeout, and retry policy.
/// </summary>
public sealed class CelesTrakCatalogSource(HttpClient http) : ICatalogSource
{
    // CelesTrak GP/OMM (full catalog, all objects including debris)
    private const string GpUrl = "https://celestrak.org/GP/GP.php?CLASS=GP&FORMAT=JSON";
    // CelesTrak SATCAT (all objects)
    private const string SatcatUrl = "https://celestrak.org/pub/satcat.json";

    public async Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default)
    {
        // Fetch in parallel to reduce wall time
        var gpTask     = http.GetStringAsync(GpUrl, ct);
        var satcatTask = http.GetStringAsync(SatcatUrl, ct);
        await Task.WhenAll(gpTask, satcatTask);

        return BuildCatalog(await gpTask, await satcatTask);
    }

    /// <summary>Parsing and join step extracted for testability without HTTP.</summary>
    internal static IReadOnlyList<CatalogObject> BuildCatalog(string gpJson, string satcatJson)
    {
        var gpRecords    = CelesTrakGpParser.Parse(gpJson);
        var satcatById   = CelesTrakSatcatParser.Parse(satcatJson);

        // Deduplicate GP records to newest-per-object (newest = highest element set number)
        var newestByNorad = new Dictionary<long, (int ElementSetNo, int Index)>(gpRecords.Count);
        for (int i = 0; i < gpRecords.Count; i++)
        {
            var r = gpRecords[i];
            if (!newestByNorad.TryGetValue(r.NoradCatId, out var existing) ||
                r.ElementSetNo > existing.ElementSetNo)
            {
                newestByNorad[r.NoradCatId] = (r.ElementSetNo, i);
            }
        }

        var asOf   = DateTime.UtcNow;
        var result = new List<CatalogObject>(newestByNorad.Count);

        foreach (var (noradId, (_, idx)) in newestByNorad)
        {
            var omm    = gpRecords[idx];
            satcatById.TryGetValue(noradId, out var satcat); // null if absent — handled gracefully
            result.Add(CatalogObjectMapper.Map(omm, satcat, asOf));
        }

        return result;
    }
}
