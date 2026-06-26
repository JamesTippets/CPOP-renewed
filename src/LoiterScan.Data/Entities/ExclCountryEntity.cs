namespace LoiterScan.Data.Entities;

/// <summary>One row per excluded country-of-origin code (spec §6.1 excl_countries).</summary>
public sealed class ExclCountryEntity
{
    public int    Id      { get; set; }
    public string Country { get; set; } = string.Empty;
}
