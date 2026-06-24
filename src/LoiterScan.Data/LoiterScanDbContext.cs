using Microsoft.EntityFrameworkCore;

namespace LoiterScan.Data;

/// <summary>EF Core context for config, runs, events, and the local catalog (SQLite).
/// Schema lives under version control via migrations; the default config is seeded from
/// config/default-config.json on first launch.</summary>
public sealed class LoiterScanDbContext : DbContext
{
    public LoiterScanDbContext(DbContextOptions<LoiterScanDbContext> options) : base(options) { }

    // TODO: DbSet<ConfigParam>, DbSet<RunRecord>, DbSet<LoiteringEventRecord>,
    //       DbSet<CatalogObjectRecord>, and exclusion-list sets.
    // Add a migration with:
    //   dotnet ef migrations add <Name> --project src/LoiterScan.Data
}
