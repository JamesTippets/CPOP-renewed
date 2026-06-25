using System.Text.Json;
using LoiterScan.Acquisition.CelesTrak.Dto;

namespace LoiterScan.Acquisition.CelesTrak;

/// <summary>Parses the CelesTrak GP OMM JSON array into <see cref="OmmRecord"/> DTOs.</summary>
internal static class CelesTrakGpParser
{
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<OmmRecord> Parse(string json)
    {
        var records = JsonSerializer.Deserialize<OmmRecord[]>(json, _opts);
        return records ?? [];
    }
}
