using System.Text.Json;
using LoiterScan.Acquisition.CelesTrak.Dto;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>Parses the CelesTrak SATCAT JSON array into a dictionary keyed by NORAD catalog ID.</summary>
internal static class CelesTrakSatcatParser
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyDictionary<long, SatcatRecord> Parse(string json)
    {
        var records = JsonSerializer.Deserialize<SatcatRecord[]>(json, _opts) ?? [];
        var dict = new Dictionary<long, SatcatRecord>(records.Length);
        foreach (var r in records)
        {
            // Keep newest if duplicate (shouldn't happen in SATCAT but guard anyway)
            dict[r.NoradCatId] = r;
        }
        return dict;
    }
}
