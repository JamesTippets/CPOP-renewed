using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.Services;

/// <summary>
/// Reads and writes the single config_params row plus exclusion lists.
/// Maps between ConfigParamEntity (DB) and RunConfig (engine domain).
/// </summary>
public sealed class ConfigService(IDbContextFactory<LoiterScanDbContext> factory)
{
    public async Task<RunConfig> GetConfigAsync()
    {
        await using var db = factory.CreateDbContext();
        var p         = await db.ConfigParams.FirstAsync();
        var countries = await db.ExclCountries.Select(x => x.Country).ToListAsync();
        var groups    = await db.ExclGroups.Select(x => x.GroupName).ToListAsync();
        var ids       = await db.ExclIds.Select(x => x.NoradId).ToListAsync();
        var pairs     = await db.ExclPairs.Select(x => new PairKey(x.PairKeyLow, x.PairKeyHigh)).ToListAsync();

        return new RunConfig(
            Cascade: new CascadeConfig(
                HorizonDays: p.HorizonDays,
                Coarse:    new TierConfig(p.CoarseStepMinutes,    p.CoarseThresholdKm),
                Fine:      new TierConfig(p.FineStepMinutes,      p.FineThresholdKm),
                Detection: new TierConfig(p.DetectionStepMinutes, p.DetectionThresholdKm),
                Buffers:   new BufferConfig(p.CoarseToFineMinutes, p.FineToDetectionMinutes),
                Loiter:    new LoiterConfig(p.LoiterMinDurationMinutes, p.LoiterExcursionAllowanceMinutes)),
            PreFilter: new PreFilterConfig(
                ExcludeDebris:         p.ExcludeDebris,
                ExcludeGroupPairsOnly: p.ExcludeGroupPairsOnly,
                RegimeScope:           p.RegimeScope,
                MaxEpochAgeDays:  p.MaxEpochAgeDays,
                ExcludedCountries: countries,
                ExcludedGroups:    groups,
                ExcludedIds:       ids,
                ExcludedPairs:     pairs),
            Acquisition: new AcquisitionConfig(p.AcquisitionSource, p.RefreshBeforeRun, p.CredentialUsername, p.CredentialPassword));
    }

    public async Task<ConfigParamEntity> GetParamEntityAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.ConfigParams.FirstAsync();
    }

    public async Task<(List<string> Countries, List<string> Groups, List<long> Ids)> GetExclusionsAsync()
    {
        await using var db = factory.CreateDbContext();
        return (
            await db.ExclCountries.Select(x => x.Country).ToListAsync(),
            await db.ExclGroups.Select(x => x.GroupName).ToListAsync(),
            await db.ExclIds.Select(x => x.NoradId).ToListAsync());
    }

    public async Task SaveParamsAsync(ConfigParamEntity updated)
    {
        await using var db = factory.CreateDbContext();
        db.ConfigParams.Update(updated);
        await db.SaveChangesAsync();
    }

    public async Task SaveExclusionsAsync(
        IEnumerable<string> countries,
        IEnumerable<string> groups,
        IEnumerable<long>   ids)
    {
        await using var db = factory.CreateDbContext();

        db.ExclCountries.RemoveRange(db.ExclCountries);
        db.ExclGroups.RemoveRange(db.ExclGroups);
        db.ExclIds.RemoveRange(db.ExclIds);

        foreach (var c in countries) db.ExclCountries.Add(new ExclCountryEntity { Country = c });
        foreach (var g in groups)    db.ExclGroups.Add(new ExclGroupEntity { GroupName = g });
        foreach (var i in ids)       db.ExclIds.Add(new ExclIdEntity { NoradId = i });

        await db.SaveChangesAsync();
    }

    public async Task<List<ExclPairEntity>> GetExclPairsAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.ExclPairs.OrderBy(x => x.PairKeyLow).ThenBy(x => x.PairKeyHigh).ToListAsync();
    }

    public async Task AddExclPairAsync(long low, long high)
    {
        await using var db = factory.CreateDbContext();
        if (await db.ExclPairs.AnyAsync(x => x.PairKeyLow == low && x.PairKeyHigh == high))
            return;
        db.ExclPairs.Add(new ExclPairEntity { PairKeyLow = low, PairKeyHigh = high });
        await db.SaveChangesAsync();
    }

    public async Task RemoveExclPairAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        await db.ExclPairs.Where(x => x.Id == id).ExecuteDeleteAsync();
    }
}
