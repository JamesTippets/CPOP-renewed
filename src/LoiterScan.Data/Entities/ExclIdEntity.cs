namespace LoiterScan.Data.Entities;

/// <summary>One row per excluded NORAD catalog ID (spec §6.1 excl_ids). Id stored as long (9-digit ready).</summary>
public sealed class ExclIdEntity
{
    public int  Id     { get; set; }
    public long NoradId { get; set; }
}
