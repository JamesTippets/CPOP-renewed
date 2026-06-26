using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LoiterScan.Data.Tests;

/// <summary>
/// Round-trip tests for the EF Core / SQLite data layer.
/// Uses an in-memory SQLite database with a persistent connection so EnsureCreatedAsync
/// creates the schema and the same connection is shared across all operations.
/// </summary>
public sealed class RunRoundTripTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;
    private LoiterScanDbContext _db = null!;

    // config/default-config.json relative to the solution root
    private static string ConfigPath =>
        Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory,
                         "..", "..", "..", "..", "..",
                         "config", "default-config.json"));

    public async Task InitializeAsync()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        var opts = new DbContextOptionsBuilder<LoiterScanDbContext>()
            .UseSqlite(_conn)
            .Options;

        _db = new LoiterScanDbContext(opts);
        await _db.Database.EnsureCreatedAsync();
        await DbInitializer.SeedAsync(_db, ConfigPath);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Seed_ConfigParams_MatchesDefaultJson()
    {
        var cfg = await _db.ConfigParams.SingleAsync();

        Assert.Equal(7,     cfg.HorizonDays);
        Assert.Equal(15,    cfg.CoarseStepMinutes);
        Assert.Equal(50.0,  cfg.CoarseThresholdKm);
        Assert.Equal(5,     cfg.FineStepMinutes);
        Assert.Equal(25.0,  cfg.FineThresholdKm);
        Assert.Equal(1,     cfg.DetectionStepMinutes);
        Assert.Equal(5.0,   cfg.DetectionThresholdKm);
        Assert.Equal(30,    cfg.CoarseToFineMinutes);
        Assert.Equal(10,    cfg.FineToDetectionMinutes);
        Assert.Equal(60,    cfg.LoiterMinDurationMinutes);
        Assert.Equal(5,     cfg.LoiterExcursionAllowanceMinutes);
        Assert.False(cfg.ExcludeDebris);
        Assert.Equal("ALL",       cfg.RegimeScope);
        Assert.Equal("celestrak", cfg.AcquisitionSource);
        Assert.True(cfg.RefreshBeforeRun);
    }

    [Fact]
    public async Task Seed_ExclGroups_ContainsStarlink()
    {
        var groups = await _db.ExclGroups.Select(g => g.GroupName).ToListAsync();
        Assert.Contains("starlink", groups);
    }

    [Fact]
    public async Task RunRoundTrip_InsertAndRead_PreservesConfigSnapshot()
    {
        const string snapshot = """{"cascade":{"horizonDays":7}}""";

        var run = new RunEntity
        {
            StartedAt         = new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            ConfigSnapshot    = snapshot,
            Status            = "completed",
            TotalPairsChecked = 1_250_000,
            EventsDetected    = 3,
        };

        _db.Runs.Add(run);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var loaded = await _db.Runs.SingleAsync(r => r.RunId == run.RunId);

        Assert.Equal(snapshot,       loaded.ConfigSnapshot);
        Assert.Equal("completed",    loaded.Status);
        Assert.Equal(1_250_000,      loaded.TotalPairsChecked);
        Assert.Equal(3,              loaded.EventsDetected);
        Assert.Equal(run.StartedAt,  loaded.StartedAt);
    }

    [Fact]
    public async Task RunRoundTrip_WithEvents_CascadeDelete()
    {
        var run = new RunEntity
        {
            StartedAt      = DateTime.UtcNow,
            ConfigSnapshot = "{}",
            Status         = "completed",
        };
        run.Events.Add(new LoiteringEventEntity
        {
            PairKeyLow       = 5,
            PairKeyHigh      = 25544,
            NoradIdA         = 5,
            NoradIdB         = 25544,
            MinRangeKm       = 3.7,
            CloseApproachUtc = DateTime.UtcNow,
            LoiterStartUtc   = DateTime.UtcNow.AddMinutes(-60),
            LoiterEndUtc     = DateTime.UtcNow,
            DurationMinutes  = 60,
            Confidence       = 0.95,
        });

        _db.Runs.Add(run);
        await _db.SaveChangesAsync();

        var eventCount = await _db.LoiteringEvents.CountAsync(e => e.RunId == run.RunId);
        Assert.Equal(1, eventCount);

        _db.Runs.Remove(run);
        await _db.SaveChangesAsync();

        var afterDelete = await _db.LoiteringEvents.CountAsync(e => e.RunId == run.RunId);
        Assert.Equal(0, afterDelete);
    }
}
