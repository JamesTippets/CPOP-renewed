using LoiterScan.Data;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

public sealed record CatalogStatus(
    int    TotalObjects,
    int    StaleObjects,
    int    DecayedObjects,
    DateTime? LastIngestedAt,
    string Source);

/// <summary>Reads aggregate catalog statistics from the local DB for the Dashboard status cards.</summary>
public sealed class CatalogStatusService(IDbContextFactory<LoiterScanDbContext> factory)
{
    private const double StaleDaysThreshold = 3.0;

    public async Task<CatalogStatus> GetStatusAsync()
    {
        await using var db = factory.CreateDbContext();

        if (!await db.CatalogObjects.AnyAsync())
            return new CatalogStatus(0, 0, 0, null, "celestrak");

        var total   = await db.CatalogObjects.CountAsync();
        var stale   = await db.CatalogObjects.CountAsync(x => x.EpochAgeDays > StaleDaysThreshold);
        var decayed = await db.CatalogObjects.CountAsync(x => x.DecayDate != null);
        var lastAt  = await db.CatalogObjects.MaxAsync(x => (DateTime?)x.IngestedAt);
        var source  = await db.CatalogObjects.Select(x => x.Source).FirstOrDefaultAsync() ?? "celestrak";

        return new CatalogStatus(total, stale, decayed, lastAt, source);
    }
}
