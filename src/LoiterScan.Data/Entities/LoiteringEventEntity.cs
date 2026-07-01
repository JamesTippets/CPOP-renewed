using System.ComponentModel.DataAnnotations.Schema;

namespace LoiterScan.Data.Entities;

/// <summary>
/// One detected loitering event, keyed to a run (spec §6.1 loitering_events).
/// PairKeyLow/High form the canonical unordered pair (min, max) — indexed for fast recurrence queries.
/// All NORAD ids stored as long (9-digit ready).
/// </summary>
public sealed class LoiteringEventEntity
{
    public long Id    { get; set; }
    public long RunId { get; set; }
    public RunEntity Run { get; set; } = null!;

    // Canonical pair key — unordered, Low ≤ High (spec §6.1)
    public long PairKeyLow  { get; set; }
    public long PairKeyHigh { get; set; }

    // The two objects (denormalised for query convenience)
    public long    NoradIdA { get; set; }
    public long    NoradIdB { get; set; }
    public string? NameA    { get; set; }
    public string? NameB    { get; set; }

    public int? PairIndex { get; set; }

    [NotMapped] public string PairKeyDisplay => PairIndex is > 0 ? $"{PairIndex}" : "—";
    [NotMapped] public string SatADisplay    => NameA is not null ? $"{NoradIdA} - {NameA}" : $"{NoradIdA}";
    [NotMapped] public string SatBDisplay    => NameB is not null ? $"{NoradIdB} - {NameB}" : $"{NoradIdB}";

    // Close-approach details
    public double   MinRangeKm       { get; set; }
    public double   CaRicR           { get; set; }
    public double   CaRicI           { get; set; }
    public double   CaRicC           { get; set; }
    public DateTime CloseApproachUtc { get; set; }
    public DateTime LoiterStartUtc   { get; set; }
    public DateTime LoiterEndUtc     { get; set; }
    public double   DurationMinutes  { get; set; }

    /// <summary>Epoch-age-derived confidence proxy (0–1).</summary>
    public double Confidence { get; set; }
}
