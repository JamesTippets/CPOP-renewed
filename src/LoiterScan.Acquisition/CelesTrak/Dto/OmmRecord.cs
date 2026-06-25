using System.Text.Json.Serialization;

namespace LoiterScan.Acquisition.CelesTrak.Dto;

/// <summary>DTO for one object from the CelesTrak GP OMM JSON array.</summary>
internal sealed class OmmRecord
{
    [JsonPropertyName("OBJECT_NAME")]   public string?  ObjectName       { get; init; }
    [JsonPropertyName("OBJECT_ID")]     public string?  IntlDesignator   { get; init; }
    [JsonPropertyName("NORAD_CAT_ID")]  public long     NoradCatId       { get; init; }
    [JsonPropertyName("EPOCH")]         public string   Epoch            { get; init; } = "";
    [JsonPropertyName("MEAN_MOTION")]   public double   MeanMotion       { get; init; }
    [JsonPropertyName("ECCENTRICITY")]  public double   Eccentricity     { get; init; }
    [JsonPropertyName("INCLINATION")]   public double   Inclination      { get; init; }
    [JsonPropertyName("RA_OF_ASC_NODE")]  public double RaOfAscNode      { get; init; }
    [JsonPropertyName("ARG_OF_PERICENTER")] public double ArgOfPericenter { get; init; }
    [JsonPropertyName("MEAN_ANOMALY")]  public double   MeanAnomaly      { get; init; }
    [JsonPropertyName("BSTAR")]         public double   Bstar            { get; init; }
    [JsonPropertyName("MEAN_MOTION_DOT")]  public double MeanMotionDot   { get; init; }
    [JsonPropertyName("MEAN_MOTION_DDOT")] public double MeanMotionDdot  { get; init; }
    [JsonPropertyName("EPHEMERIS_TYPE")] public int     EphemerisType    { get; init; }
    [JsonPropertyName("CLASSIFICATION_TYPE")] public string? Classification { get; init; }
    [JsonPropertyName("ELEMENT_SET_NO")] public int     ElementSetNo     { get; init; }
    [JsonPropertyName("REV_AT_EPOCH")]  public int      RevAtEpoch       { get; init; }
}
