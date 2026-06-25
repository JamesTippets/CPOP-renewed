using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.Data;

/// <summary>
/// EF Core context for config, runs, events, and the local catalog (SQLite).
/// Schema lives under version control via migrations; the default config is seeded from
/// config/default-config.json on first launch.
/// </summary>
public sealed class LoiterScanDbContext : DbContext
{
    public LoiterScanDbContext(DbContextOptions<LoiterScanDbContext> options) : base(options) { }

    public DbSet<ConfigParamEntity>    ConfigParams    { get; set; } = null!;
    public DbSet<ExclCountryEntity>    ExclCountries   { get; set; } = null!;
    public DbSet<ExclGroupEntity>      ExclGroups      { get; set; } = null!;
    public DbSet<ExclIdEntity>         ExclIds         { get; set; } = null!;
    public DbSet<RunEntity>            Runs            { get; set; } = null!;
    public DbSet<LoiteringEventEntity> LoiteringEvents { get; set; } = null!;
    public DbSet<CatalogObjectEntity>  CatalogObjects  { get; set; } = null!;
    public DbSet<CatalogGroupEntity>   CatalogGroups   { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── ConfigParams ──────────────────────────────────────────────────────
        mb.Entity<ConfigParamEntity>(e =>
        {
            e.ToTable("config_params");
            e.HasKey(x => x.Id);
        });

        // ── Exclusion lists ───────────────────────────────────────────────────
        mb.Entity<ExclCountryEntity>(e =>
        {
            e.ToTable("excl_countries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Country).IsUnique();
        });

        mb.Entity<ExclGroupEntity>(e =>
        {
            e.ToTable("excl_groups");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GroupName).IsUnique();
        });

        mb.Entity<ExclIdEntity>(e =>
        {
            e.ToTable("excl_ids");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NoradId).IsUnique();
        });

        // ── Runs ──────────────────────────────────────────────────────────────
        mb.Entity<RunEntity>(e =>
        {
            e.ToTable("runs");
            e.HasKey(x => x.RunId);
            e.Property(x => x.RunId).ValueGeneratedOnAdd();
            e.Property(x => x.Status).HasMaxLength(32);
            e.Property(x => x.Trigger).HasMaxLength(64);
            // EF treats string columns as TEXT — ConfigSnapshot can be large
            e.Property(x => x.ConfigSnapshot).HasColumnType("TEXT");

            e.HasMany(x => x.Events)
             .WithOne(x => x.Run)
             .HasForeignKey(x => x.RunId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── LoiteringEvents ───────────────────────────────────────────────────
        mb.Entity<LoiteringEventEntity>(e =>
        {
            e.ToTable("loitering_events");
            e.HasKey(x => x.Id);
            // Composite index on canonical pair key — fast recurrence lookup
            e.HasIndex(x => new { x.PairKeyLow, x.PairKeyHigh }).HasDatabaseName("ix_event_pair_key");
            e.HasIndex(x => x.RunId);
        });

        // ── CatalogObjects ────────────────────────────────────────────────────
        mb.Entity<CatalogObjectEntity>(e =>
        {
            e.ToTable("catalog_objects");
            e.HasKey(x => x.NoradId);

            e.HasMany(x => x.Groups)
             .WithOne(x => x.CatalogObject)
             .HasForeignKey(x => x.NoradId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CatalogGroups ─────────────────────────────────────────────────────
        mb.Entity<CatalogGroupEntity>(e =>
        {
            e.ToTable("catalog_groups");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.NoradId, x.GroupName }).IsUnique();
            e.Property(x => x.GroupName).HasMaxLength(128);
        });
    }
}
