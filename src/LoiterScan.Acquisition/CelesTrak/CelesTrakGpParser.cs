using System.Text.Json;
using LoiterScan.Acquisition.CelesTrak.Dto;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>Parses the CelesTrak GP OMM JSON array into <see cref="OmmRecord"/> DTOs.</summary>
internal static class CelesTrakGpParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        // Space-Track returns all numeric fields as quoted strings; AllowReadingFromString
        // accepts both "14.34" and 14.34 so the same parser works for both sources.
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public static IReadOnlyList<OmmRecord> Parse(string json)
    {
        var records = JsonSerializer.Deserialize<OmmRecord[]>(json, _opts);
        return records ?? [];
    }
}
