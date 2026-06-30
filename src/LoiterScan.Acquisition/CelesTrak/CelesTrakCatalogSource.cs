using LoiterScan.Acquisition.CelesTrak.Dto;
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
    // CelesTrak GP/OMM — active catalog in OMM JSON format
    // (replaced the old /GP/GP.php?CLASS=GP path which was retired ~2023)
    private const string GpUrl = "https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=JSON";

    // CelesTrak SATCAT — joins owner, object-type, and decay metadata
    // Fetched opportunistically: if this endpoint is unavailable those fields are left null.
    private const string SatcatUrl = "https://www.celestrak.org/pub/satcat.json";

    public async Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default)
    {
        // Start both requests concurrently; SATCAT failure is non-fatal
        var gpTask     = http.GetStringAsync(GpUrl, ct);
        var satcatTask = TryFetchSatcatAsync(ct);

        // Both tasks are already running; awaiting sequentially preserves parallelism
        string  gpJson = await gpTask;
        string? satcat = await satcatTask;
        return BuildCatalog(gpJson, satcat);
    }

    private async Task<string?> TryFetchSatcatAsync(CancellationToken ct)
    {
        try
        {
            return await http.GetStringAsync(SatcatUrl, ct);
        }
        catch (Exception)
        {
            // SATCAT endpoint unavailable — proceed without owner/type metadata
            return null;
        }
    }

    /// <summary>Parsing and join step extracted for testability without HTTP.</summary>
    internal static IReadOnlyList<CatalogObject> BuildCatalog(string gpJson, string? satcatJson)
    {
        var gpRecords    = CelesTrakGpParser.Parse(gpJson);
        IReadOnlyDictionary<long, SatcatRecord> satcatById = satcatJson is not null
            ? CelesTrakSatcatParser.Parse(satcatJson)
            : new Dictionary<long, SatcatRecord>();

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
