using LoiterScan.Acquisition.CelesTrak;
using LoiterScan.Acquisition.SpaceTrack;
using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

/// <summary>
/// Manages live catalog refresh with a 2-hour rate limit.
/// When RefreshBeforeRun is true, checks whether the cached catalog is stale
/// (older than 2 hours or empty) before calling the configured live source.
/// Fetched objects are bulk-upserted into the CatalogObjects table so that
/// subsequent pipeline runs read from the DB via DbCatalogSource.
/// </summary>
public sealed class CatalogCacheService(
    CelesTrakCatalogSource     celestrak,
    SpaceTrackCatalogSource    spaceTrack,
    IDbContextFactory<LoiterScanDbContext> factory)
{
    private const int RateLimitHours = 2;

    public async Task EnsureFreshAsync(AcquisitionConfig acq, CancellationToken ct)
    {
        if (!await IsStaleAsync(ct))
            return;

        IReadOnlyList<CatalogObject> objects;
        if (acq.Source.Equals("space-track", StringComparison.OrdinalIgnoreCase))
        {
            var user = acq.Username ?? string.Empty;
            var pass = acq.Password ?? string.Empty;
            objects = await spaceTrack.FetchCatalogAsync(user, pass, ct);
        }
        else
        {
            objects = await celestrak.FetchCatalogAsync(ct);
        }

        await UpsertAsync(objects, acq.Source, ct);
    }

    private async Task<bool> IsStaleAsync(CancellationToken ct)
    {
        await using var db = factory.CreateDbContext();
        var lastAt = await db.CatalogObjects.MaxAsync(o => (DateTime?)o.IngestedAt, ct);
        return lastAt is null || (DateTime.UtcNow - lastAt.Value).TotalHours >= RateLimitHours;
    }

    private async Task UpsertAsync(IReadOnlyList<CatalogObject> objects, string source, CancellationToken ct)
    {
        await using var db = factory.CreateDbContext();
        var now = DateTime.UtcNow;

        // Full replace: delete all existing rows then bulk-insert the fresh catalog.
        // The table is local SQLite with no concurrent writers so this is safe.
        await db.CatalogGroups.ExecuteDeleteAsync(ct);
        await db.CatalogObjects.ExecuteDeleteAsync(ct);

        db.CatalogObjects.AddRange(objects.Select(o => new CatalogObjectEntity
        {
            NoradId              = o.NoradId,
            Name                 = o.Name,
            IntlDesignator       = o.IntlDesignator,
            MeanMotionRevPerDay  = o.Elements.MeanMotionRevPerDay,
            Eccentricity         = o.Elements.Eccentricity,
            InclinationDeg       = o.Elements.InclinationDeg,
            RaanDeg              = o.Elements.RaanDeg,
            ArgPerigeeDeg        = o.Elements.ArgPerigeeDeg,
            MeanAnomalyDeg       = o.Elements.MeanAnomalyDeg,
            BStar                = o.Elements.BStar,
            EpochUtc             = o.Elements.EpochUtc,
            EphemerisType        = 0,
            MeanMotionDotRevPerDay  = 0,
            MeanMotionDdotRevPerDay = 0,
            Owner                = o.Owner,
            ObjectType           = o.ObjectType,
            IsDebris             = o.IsDebris,
            DecayDate            = o.DecayDate,
            SemiMajorAxisKm      = (o.ApogeeKm + o.PerigeeKm) / 2.0,
            ApogeeKm             = o.ApogeeKm,
            PerigeeKm            = o.PerigeeKm,
            Regime               = o.Regime.ToString(),
            EpochAgeDays         = o.EpochAgeDays,
            IngestedAt           = now,
            Source               = source,
        }));

        await db.SaveChangesAsync(ct);
    }
}
