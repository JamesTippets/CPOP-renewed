namespace LoiterScan.Core.Models;

public enum OrbitRegime { Unknown, Leo, Meo, Geo, Heo }

/// <summary>A catalog object: elements plus SATCAT metadata and fields derived on ingest.</summary>
public sealed record CatalogObject(
    long NoradId,
    string? Name,
    string? IntlDesignator,
    MeanElements Elements,
    string? Owner,
    string? ObjectType,
    bool IsDebris,
    DateTime? DecayDate,
    double ApogeeKm,
    double PerigeeKm,
    OrbitRegime Regime,
    double EpochAgeDays);
