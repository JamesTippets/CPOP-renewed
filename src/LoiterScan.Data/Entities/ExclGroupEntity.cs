namespace LoiterScan.Data.Entities;

/// <summary>One row per excluded CelesTrak GROUP name (spec §6.1 excl_groups).</summary>
public sealed class ExclGroupEntity
{
    public int    Id        { get; set; }
    public string GroupName { get; set; } = string.Empty;
}
