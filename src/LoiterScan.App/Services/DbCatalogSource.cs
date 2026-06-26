using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

/// <summary>
/// Reads the local catalog from the CatalogObjects DB table.
/// Used by DetectionPipeline to run entirely off the database cache —
/// the live fetch is handled separately by CatalogCacheService.
/// </summary>
public sealed class DbCatalogSource(IDbContextFactory<LoiterScanDbContext> factory) : ICatalogSource
{
    public async Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default)
    {
        await using var db = factory.CreateDbContext();
        var entities = await db.CatalogObjects.AsNoTracking().ToListAsync(ct);
        return entities.Select(MapToCore).ToList();
    }

    private static CatalogObject MapToCore(CatalogObjectEntity e) =>
        new(
            NoradId:        e.NoradId,
            Name:           e.Name,
            IntlDesignator: e.IntlDesignator,
            Elements: new MeanElements(
                MeanMotionRevPerDay: e.MeanMotionRevPerDay,
                Eccentricity:        e.Eccentricity,
                InclinationDeg:      e.InclinationDeg,
                RaanDeg:             e.RaanDeg,
                ArgPerigeeDeg:       e.ArgPerigeeDeg,
                MeanAnomalyDeg:      e.MeanAnomalyDeg,
                BStar:               e.BStar,
                EpochUtc:            e.EpochUtc),
            Owner:        e.Owner,
            ObjectType:   e.ObjectType,
            IsDebris:     e.IsDebris,
            DecayDate:    e.DecayDate,
            ApogeeKm:     e.ApogeeKm,
            PerigeeKm:    e.PerigeeKm,
            Regime:       Enum.TryParse<OrbitRegime>(e.Regime, out var r) ? r : OrbitRegime.Unknown,
            EpochAgeDays: e.EpochAgeDays);
}
