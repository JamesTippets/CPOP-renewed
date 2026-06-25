namespace LoiterScan.Data.Entities;

/// <summary>
/// Single-row table that holds all editable cascade and pre-filter parameters (spec §6.1).
/// Seeded from config/default-config.json on first launch; every run snapshot is a JSON copy.
/// </summary>
public sealed class ConfigParamEntity
{
    public int Id { get; set; }

    // Cascade — horizon
    public int HorizonDays { get; set; }

    // Cascade — coarse tier
    public int    CoarseStepMinutes   { get; set; }
    public double CoarseThresholdKm   { get; set; }

    // Cascade — fine tier
    public int    FineStepMinutes     { get; set; }
    public double FineThresholdKm     { get; set; }

    // Cascade — detection tier
    public int    DetectionStepMinutes  { get; set; }
    public double DetectionThresholdKm  { get; set; }

    // Cascade — inter-tier buffers
    public int CoarseToFineMinutes      { get; set; }
    public int FineToDetectionMinutes   { get; set; }

    // Loiter definition
    public int LoiterMinDurationMinutes        { get; set; }
    public int LoiterExcursionAllowanceMinutes { get; set; }

    // Pre-filter
    public bool   ExcludeDebris  { get; set; }
    public string RegimeScope    { get; set; } = "ALL";

    // Acquisition
    public string AcquisitionSource  { get; set; } = "celestrak";
    public bool   RefreshBeforeRun   { get; set; }
}
