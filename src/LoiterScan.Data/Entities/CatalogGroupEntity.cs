namespace LoiterScan.Data.Entities;

/// <summary>
/// CelesTrak GROUP membership for a catalog object (spec §4.3).
/// e.g. GroupName = "starlink", "oneweb".  Drives constellation exclusion.
/// </summary>
public sealed class CatalogGroupEntity
{
    public int    Id        { get; set; }
    public long   NoradId   { get; set; }
    public string GroupName { get; set; } = string.Empty;

    public CatalogObjectEntity CatalogObject { get; set; } = null!;
}
