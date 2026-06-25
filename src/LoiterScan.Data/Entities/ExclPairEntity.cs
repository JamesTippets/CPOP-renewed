namespace LoiterScan.Data.Entities;

/// <summary>One row per excluded co-orbital/docked pair (spec §6.1 excl_pairs). Keys are in canonical low/high order.</summary>
public sealed class ExclPairEntity
{
    public int  Id          { get; set; }
    public long PairKeyLow  { get; set; }
    public long PairKeyHigh { get; set; }
}
