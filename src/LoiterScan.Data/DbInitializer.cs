using System.Text.Json;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.Data;

/// <summary>
/// Seeds the config_params and exclusion-list tables from config/default-config.json
/// when the database is first created (or is empty).
/// Call ApplyAndSeedAsync after EnsureCreatedAsync / running migrations.
/// </summary>
public static class DbInitializer
{
    /// <summary>Runs pending migrations then seeds the default config (production path).</summary>
    public static async Task ApplyAndSeedAsync(LoiterScanDbContext db, string configJsonPath)
    {
        await db.Database.MigrateAsync();
        await SeedAsync(db, configJsonPath);
    }

    /// <summary>Seeds without running migrations (test path: call after EnsureCreatedAsync).</summary>
    public static async Task SeedAsync(LoiterScanDbContext db, string configJsonPath)
    {
        if (await db.ConfigParams.AnyAsync())
            return; // already seeded

        var json = await File.ReadAllTextAsync(configJsonPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var cascade   = root.GetProperty("cascade");
        var coarse    = cascade.GetProperty("coarse");
        var fine      = cascade.GetProperty("fine");
        var detection = cascade.GetProperty("detection");
        var buffers   = cascade.GetProperty("buffers");
        var loiter    = cascade.GetProperty("loiter");
        var preFilter = root.GetProperty("preFilter");
        var acq       = root.GetProperty("acquisition");

        db.ConfigParams.Add(new ConfigParamEntity
        {
            Id = 1,

            HorizonDays = cascade.GetProperty("horizonDays").GetInt32(),

            CoarseStepMinutes  = coarse.GetProperty("stepMinutes").GetInt32(),
            CoarseThresholdKm  = coarse.GetProperty("thresholdKm").GetDouble(),

            FineStepMinutes    = fine.GetProperty("stepMinutes").GetInt32(),
            FineThresholdKm    = fine.GetProperty("thresholdKm").GetDouble(),

            DetectionStepMinutes = detection.GetProperty("stepMinutes").GetInt32(),
            DetectionThresholdKm = detection.GetProperty("thresholdKm").GetDouble(),

            CoarseToFineMinutes     = buffers.GetProperty("coarseToFineMinutes").GetInt32(),
            FineToDetectionMinutes  = buffers.GetProperty("fineToDetectionMinutes").GetInt32(),

            LoiterMinDurationMinutes        = loiter.GetProperty("minDurationMinutes").GetInt32(),
            LoiterExcursionAllowanceMinutes = loiter.GetProperty("excursionAllowanceMinutes").GetInt32(),

            ExcludeDebris         = preFilter.GetProperty("excludeDebris").GetBoolean(),
            ExcludeGroupPairsOnly = preFilter.TryGetProperty("excludeGroupPairsOnly", out var egpo) && egpo.GetBoolean(),
            RegimeScope           = preFilter.GetProperty("regimeScope").GetString() ?? "ALL",
            MaxEpochAgeDays = preFilter.TryGetProperty("maxEpochAgeDays", out var mea) ? mea.GetInt32() : 14,

            AcquisitionSource   = acq.GetProperty("source").GetString() ?? "celestrak",
            RefreshBeforeRun    = acq.GetProperty("refreshBeforeRun").GetBoolean(),
            CredentialUsername  = acq.TryGetProperty("username", out var u) ? u.GetString() : null,
            CredentialPassword  = acq.TryGetProperty("password", out var pw) ? pw.GetString() : null,
        });

        foreach (var country in preFilter.GetProperty("excludedCountries").EnumerateArray())
            db.ExclCountries.Add(new ExclCountryEntity { Country = country.GetString()! });

        foreach (var group in preFilter.GetProperty("excludedGroups").EnumerateArray())
            db.ExclGroups.Add(new ExclGroupEntity { GroupName = group.GetString()! });

        foreach (var id in preFilter.GetProperty("excludedIds").EnumerateArray())
            db.ExclIds.Add(new ExclIdEntity { NoradId = id.GetInt64() });

        await db.SaveChangesAsync();
    }
}
