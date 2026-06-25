namespace LoiterScan.Data.Entities;

/// <summary>
/// Local catalog row: OMM mean elements + SATCAT metadata + derived fields (spec §4.3).
/// NoradId is long so it accommodates 9-digit catalog numbers (≥ 100 000) expected mid-2026.
/// </summary>
public sealed class CatalogObjectEntity
{
    public long    NoradId         { get; set; }
    public string? Name            { get; set; }
    public string? IntlDesignator  { get; set; }

    // OMM mean elements
    public double   MeanMotionRevPerDay { get; set; }
    public double   Eccentricity        { get; set; }
    public double   InclinationDeg      { get; set; }
    public double   RaanDeg             { get; set; }
    public double   ArgPerigeeDeg       { get; set; }
    public double   MeanAnomalyDeg      { get; set; }
    public double   BStar               { get; set; }
    public DateTime EpochUtc            { get; set; }

    // GP metadata
    public int    EphemerisType           { get; set; }
    public double MeanMotionDotRevPerDay  { get; set; }
    public double MeanMotionDdotRevPerDay { get; set; }

    // SATCAT metadata
    public string?   Owner      { get; set; }
    public string?   ObjectType { get; set; }
    public bool      IsDebris   { get; set; }
    public DateTime? DecayDate  { get; set; }

    // Derived fields computed on ingest (spec §4.3)
    public double SemiMajorAxisKm { get; set; }
    public double ApogeeKm        { get; set; }
    public double PerigeeKm       { get; set; }

    /// <summary>Stored as string to avoid EF enum-mapping friction; convert via Enum.Parse on read.</summary>
    public string Regime       { get; set; } = "Unknown";
    public double EpochAgeDays { get; set; }

    // Ingestion audit
    public DateTime IngestedAt { get; set; }
    public string   Source     { get; set; } = "celestrak";

    public ICollection<CatalogGroupEntity> Groups { get; set; } = [];
}
