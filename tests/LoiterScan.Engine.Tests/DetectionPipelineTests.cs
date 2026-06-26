using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Engine;
using Xunit;

namespace LoiterScan.Engine.Tests;

/// <summary>
/// Integration tests for the four-tier detection cascade.
/// All propagation is faked so tests are purely algorithmic â€” no SGP4 needed here.
/// </summary>
public class DetectionPipelineTests
{
    // â”€â”€ Shared infrastructure â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    // Fixed T0 so tests are deterministic
    private static readonly DateTime T0 =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RunConfig MakeConfig(int horizonDays = 1) => new(
        Cascade: new CascadeConfig(
            HorizonDays:   horizonDays,
            Coarse:        new TierConfig(15, 50),
            Fine:          new TierConfig(5,  25),
            Detection:     new TierConfig(1,   5),
            Buffers:       new BufferConfig(30, 10),
            Loiter:        new LoiterConfig(60, 5)),
        PreFilter: new PreFilterConfig(
            ExcludeDebris:        false,
            ExcludeGroupPairsOnly: false,
            RegimeScope:          "ALL",
            MaxEpochAgeDays:   9999,   // synthetic test epochs are fixed in the past
            ExcludedCountries: [],
            ExcludedGroups:    [],
            ExcludedIds:       [],
            ExcludedPairs:     []),
        Acquisition: new AcquisitionConfig("celestrak", false));

    // Synthetic CatalogObject â€” elements are dummies; the fake propagator ignores them
    // and positions by EpochUtc (used as an object identity token).
    private static CatalogObject MakeObject(
        long noradId, double apogeeKm, double perigeeKm,
        DateTime epoch, string? name = null) =>
        new(noradId, name ?? $"SAT-{noradId}", null,
            new MeanElements(15.0, 0.001, 51.6, 0, 0, 0, 1e-4, epoch),
            null, "PAYLOAD", false, null,
            apogeeKm, perigeeKm, OrbitRegime.Leo, 0.5);

    // Fake ICatalogSource backed by a fixed list
    private sealed class FakeCatalog(IReadOnlyList<CatalogObject> items) : ICatalogSource
    {
        public Task<IReadOnlyList<CatalogObject>> FetchCatalogAsync(CancellationToken ct = default)
            => Task.FromResult(items);
    }

    // Fake IPropagator: positions keyed by epoch (used as object identity)
    private sealed class FakePropagator(
        Dictionary<DateTime, Func<DateTime, (double X, double Y, double Z)>> positions)
        : IPropagator
    {
        public OrbitState Propagate(MeanElements e, DateTime at)
        {
            if (positions.TryGetValue(e.EpochUtc, out var fn))
            {
                var (x, y, z) = fn(at);
                return new OrbitState(at, x, y, z, 0, 0, 0);
            }
            return new OrbitState(at, 7000, 0, 0, 0, 0, 0);
        }

        public bool TryPropagate(MeanElements e, DateTime at, out OrbitState state)
        {
            state = Propagate(e, at);
            return true;
        }
    }

    // â”€â”€ Test 1: loitering pair is detected â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_DetectsKnownLoiterPair()
    {
        // A and B are always 3 km apart â†’ well within 5 km threshold for the full horizon
        var epochA = T0.AddMinutes(0);
        var epochB = T0.AddMinutes(1);

        var objA = MakeObject(1, 7001, 6999, epochA, "OBJ-A");
        var objB = MakeObject(2, 7001, 6999, epochB, "OBJ-B");

        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochA] = _ => (7000, 0, 0),
            [epochB] = _ => (7003, 0, 0),   // 3 km from A at all times
        });

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objA, objB]));
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 1), T0);

        Assert.Single(result.Events);
        var ev = result.Events[0];
        Assert.Equal(new PairKey(1, 2), ev.Pair);
        Assert.InRange(ev.MinRangeKm, 0, 5);
        Assert.True(ev.DurationMinutes >= 60, $"Expected â‰¥ 60 min, got {ev.DurationMinutes}");
    }

    // â”€â”€ Test 2: flyby (< 1 h close) is NOT detected â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_DoesNotDetect_ShortFlyby()
    {
        // C and D are within 3 km for only 45 minutes, then D jumps far away
        var epochC = T0.AddMinutes(2);
        var epochD = T0.AddMinutes(3);

        var objC = MakeObject(3, 7001, 6999, epochC, "OBJ-C");
        var objD = MakeObject(4, 7001, 6999, epochD, "OBJ-D");

        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochC] = _ => (7000, 0, 0),
            [epochD] = at =>
            {
                double elapsed = (at - T0).TotalMinutes;
                return (elapsed >= 0 && elapsed < 45)
                    ? (7003, 0, 0)    // 3 km from C for first 45 min only
                    : (8000, 0, 0);   // far from C (1000 km)
            },
        });

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objC, objD]));
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 1), T0);

        Assert.Empty(result.Events);
    }

    // â”€â”€ Test 3: bridged excursion extending the qualifying span â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_BridgesShortExcursion_AndDetectsEvent()
    {
        // E and F are within 3 km for 55 min, then 4-min excursion > 5 km, then 10 more min â‰¤ 5 km
        // Total qualifying span â‰¥ 60 min (55 + 4 bridge + 10 = 69 min elapsed â†’ event)
        var epochE = T0.AddMinutes(4);
        var epochF = T0.AddMinutes(5);

        var objE = MakeObject(5, 7001, 6999, epochE, "OBJ-E");
        var objF = MakeObject(6, 7001, 6999, epochF, "OBJ-F");

        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochE] = _ => (7000, 0, 0),
            [epochF] = at =>
            {
                double m = (at - T0).TotalMinutes;
                if (m < 55)  return (7003, 0, 0);   // within 5 km
                if (m < 59)  return (7010, 0, 0);   // 4-min excursion (â‰¤ 5-min allowance â†’ bridged)
                return             (7003, 0, 0);     // back within 5 km for 10+ more min
            },
        });

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objE, objF]));
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 1), T0);

        Assert.Single(result.Events);
        Assert.True(result.Events[0].DurationMinutes >= 60);
    }

    // â”€â”€ Test 4: long excursion resets the timer (no event) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_LongExcursionResetsTimer_NoEvent()
    {
        // G and H are within 5 km for 30 min, then 10-min excursion (> 5-min allowance â†’ reset),
        // then another 50 min within 5 km. Neither span reaches 60 min.
        var epochG = T0.AddMinutes(6);
        var epochH = T0.AddMinutes(7);

        var objG = MakeObject(7, 7001, 6999, epochG, "OBJ-G");
        var objH = MakeObject(8, 7001, 6999, epochH, "OBJ-H");

        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochG] = _ => (7000, 0, 0),
            [epochH] = at =>
            {
                double m = (at - T0).TotalMinutes;
                if (m >= 0 && m < 30)  return (7003, 0, 0);   // 30 min in range
                if (m >= 30 && m < 40) return (7060, 0, 0);   // 10-min excursion â†’ timer reset
                if (m >= 40 && m < 90) return (7003, 0, 0);   // 50 min in range (< 60)
                return                        (7200, 0, 0);   // far (200 km) â€” times before T0 and after T0+90min
            },
        });

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objG, objH]));
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 1), T0);

        Assert.Empty(result.Events);
    }

    // â”€â”€ Test 5: pre-filter exclusions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_ExcludesDebrisWhenToggleOn()
    {
        var epochX = T0.AddMinutes(8);
        var epochY = T0.AddMinutes(9);

        // X is debris; Y is payload. They would loiter if X wasn't filtered.
        var objX = MakeObject(9,  7001, 6999, epochX) with { IsDebris = true, ObjectType = "DEBRIS" };
        var objY = MakeObject(10, 7001, 6999, epochY);

        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochX] = _ => (7000, 0, 0),
            [epochY] = _ => (7003, 0, 0),
        });

        var config = MakeConfig(1) with
        {
            PreFilter = MakeConfig(1).PreFilter with { ExcludeDebris = true }
        };

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objX, objY]));
        var result = await pipeline.RunAsync(config, T0);

        Assert.Empty(result.Events);  // X filtered out â†’ no pair â†’ no event
    }

    // â”€â”€ Test 6: apogee/perigee gating eliminates impossible pairs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_ApogeePerigeeGating_EliminatesImpossiblePair()
    {
        // A' at ~400 km altitude (radius ~6771 km), B' at ~1200 km altitude (radius ~7571 km)
        // Difference > 50 km coarse threshold â†’ gated out before any propagation.
        var epochAp = T0.AddMinutes(10);
        var epochBp = T0.AddMinutes(11);

        var objAp = MakeObject(11, 6800, 6750, epochAp);   // apogee 6800, perigee 6750
        var objBp = MakeObject(12, 7600, 7550, epochBp);   // apogee 7600, perigee 7550

        // If gating works, propagator is never called for these two as a pair.
        int propagations = 0;
        var propagator = new FakePropagator(new Dictionary<DateTime, Func<DateTime, (double, double, double)>>
        {
            [epochAp] = at => { propagations++; return (6780, 0, 0); },
            [epochBp] = at => { propagations++; return (7580, 0, 0); },
        });

        var pipeline = new DetectionPipeline(propagator, new FakeCatalog([objAp, objBp]));
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 1), T0);

        Assert.Empty(result.Events);
        // Gating eliminates the pair after coarse; we don't assert zero propagations
        // since coarse propagates all objects, but we confirm no events are produced.
    }

    // â”€â”€ Test 7: coarse-pass performance sanity check â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Cascade_CoarsePass_CompletesInReasonableTime_For500Objects()
    {
        const int N = 500;

        // 500 objects spread on a sphere at 7000 km radius.
        // Angular separation â‰ˆ 1.6Â° â‰ˆ 1100 km â€” well above the 50 km coarse threshold.
        // No pairs will be flagged, so fine and detection tiers do zero propagations.
        var objects = new List<CatalogObject>(N);
        var posDict = new Dictionary<DateTime, Func<DateTime, (double, double, double)>>();

        for (int i = 0; i < N; i++)
        {
            var epoch = T0.AddSeconds(i);  // unique epoch = object identity token
            double phi   = Math.PI * i / N;
            double theta = 2 * Math.PI * i / N * 1.618033988;  // golden angle spread
            double r = 7000;
            double x = r * Math.Sin(phi) * Math.Cos(theta);
            double y = r * Math.Sin(phi) * Math.Sin(theta);
            double z = r * Math.Cos(phi);

            objects.Add(MakeObject(1000 + i, 7001, 6999, epoch));

            var capturedX = x; var capturedY = y; var capturedZ = z;
            posDict[epoch] = _ => (capturedX, capturedY, capturedZ);
        }

        var propagator = new FakePropagator(posDict);
        var pipeline   = new DetectionPipeline(propagator, new FakeCatalog(objects));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        // Use 7-day horizon (full spec horizon) to validate performance at scale
        var result = await pipeline.RunAsync(MakeConfig(horizonDays: 7), T0);
        sw.Stop();

        Assert.Empty(result.Events);                                          // no close pairs
        Assert.True(sw.Elapsed.TotalSeconds < 15,
            $"Coarse pass took {sw.Elapsed.TotalSeconds:F1}s â€” expected < 15s");
    }
}


