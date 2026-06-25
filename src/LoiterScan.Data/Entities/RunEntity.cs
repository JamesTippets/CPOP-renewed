namespace LoiterScan.Data.Entities;

/// <summary>
/// Persisted run record (spec §6.1 runs table).
/// ConfigSnapshot is a JSON-serialized copy of the full RunConfig at the time of the run,
/// ensuring results remain reproducible even if the live config is later changed.
/// </summary>
public sealed class RunEntity
{
    public long     RunId    { get; set; }
    public DateTime StartedAt { get; set; }

    /// <summary>"on-demand" in v1; column retained for future automation.</summary>
    public string Trigger { get; set; } = "on-demand";

    public DateTime? CatalogEpochWindowStart { get; set; }
    public DateTime? CatalogEpochWindowEnd   { get; set; }

    /// <summary>JSON snapshot of the full RunConfig that produced this run.</summary>
    public string ConfigSnapshot { get; set; } = "{}";

    /// <summary>"pending" | "running" | "completed" | "cancelled" | "failed"</summary>
    public string Status { get; set; } = "pending";

    public int? TotalPairsChecked { get; set; }
    public int? EventsDetected    { get; set; }

    public ICollection<LoiteringEventEntity> Events { get; set; } = [];
}
