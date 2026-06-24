namespace LoiterScan.Core.Models;

/// <summary>Parameters for one cascade tier (mirrors default-config.json cascade.coarse/fine/detection).</summary>
public sealed record TierConfig(int StepMinutes, double ThresholdKm);

/// <summary>Window buffers carried between cascade tiers.</summary>
public sealed record BufferConfig(int CoarseToFineMinutes, int FineToDetectionMinutes);

/// <summary>Loitering duration and excursion rules (spec §1.1).</summary>
public sealed record LoiterConfig(int MinDurationMinutes, int ExcursionAllowanceMinutes);

/// <summary>Full cascade configuration (spec §5, §6.1 config_params).</summary>
public sealed record CascadeConfig(
    int HorizonDays,
    TierConfig Coarse,
    TierConfig Fine,
    TierConfig Detection,
    BufferConfig Buffers,
    LoiterConfig Loiter);

/// <summary>Pre-filter settings (spec §5.3).</summary>
public sealed record PreFilterConfig(
    bool ExcludeDebris,
    string RegimeScope,
    IReadOnlyList<string> ExcludedCountries,
    IReadOnlyList<string> ExcludedGroups,
    IReadOnlyList<long> ExcludedIds);

/// <summary>Acquisition settings (spec §4).</summary>
public sealed record AcquisitionConfig(string Source, bool RefreshBeforeRun);

/// <summary>Complete application configuration snapshot — stored per run for auditability (spec §6).</summary>
public sealed record RunConfig(
    CascadeConfig Cascade,
    PreFilterConfig PreFilter,
    AcquisitionConfig Acquisition);
