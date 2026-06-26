using System.Text.Json.Serialization;

namespace LoiterScan.Acquisition.CelesTrak.Dto;

/// <summary>DTO for one row from the CelesTrak SATCAT JSON array.</summary>
internal sealed class SatcatRecord
{
    [JsonPropertyName("NORAD_CAT_ID")]  public long     NoradCatId   { get; init; }
    [JsonPropertyName("SATNAME")]       public string?  SatName      { get; init; }
    [JsonPropertyName("COUNTRY")]       public string?  Country      { get; init; }
    [JsonPropertyName("OBJECT_TYPE")]   public string?  ObjectType   { get; init; }
    [JsonPropertyName("DECAY")]         public string?  Decay        { get; init; }
    [JsonPropertyName("OPS_STATUS_CODE")] public string? OpsStatus  { get; init; }
}
