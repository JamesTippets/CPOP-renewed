using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoiterScan.App.Services;
using LoiterScan.Core.Abstractions;
using LoiterScan.Core.Models;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Event-detail view-model: computes RIC relative-motion data and range-vs-time series
/// for the selected loitering event. The view's code-behind calls BuildPlots() after the VM loads.
/// </summary>
public sealed partial class EventDetailViewModel : ObservableObject
{
    private readonly IDbContextFactory<LoiterScanDbContext> _factory;
    private readonly IPropagator _propagator;

    [ObservableProperty] private LoiteringEventEntity? _event;
    [ObservableProperty] private string _objectALabel = "—";
    [ObservableProperty] private string _objectBLabel = "—";
    [ObservableProperty] private double _minRangeKm;
    [ObservableProperty] private string _closeApproach = "—";
    [ObservableProperty] private double _durationMinutes;
    [ObservableProperty] private double _confidence;

    // Plot data exposed as arrays; the view code-behind applies these to WpfPlot.
    public double[]? RicR  { get; private set; }
    public double[]? RicI  { get; private set; }
    public double[]? RicC  { get; private set; }

    public double[]? RangeTimeOADate  { get; private set; }
    public double[]? RangeKm          { get; private set; }
    public double    LoiterStartOA    { get; private set; }
    public double    LoiterEndOA      { get; private set; }
    public double    ThresholdKm { get; } = 5.0;

    public (double R, double I, DateTime TimeUtc)[] RicLabelPoints { get; private set; } = [];

    public bool HasData { get; private set; }

    // Cesium orbital path data (flat [x0,y0,z0, x1,y1,z1, …] in metres)
    public double[]? OrbitEcefA    { get; private set; }
    public double[]? OrbitEcefB    { get; private set; }
    public double[]? OrbitEciA     { get; private set; }
    public double[]? OrbitEciB     { get; private set; }
    public double[]  SatALabelEcef { get; private set; } = [];
    public double[]  SatBLabelEcef { get; private set; } = [];
    public double[]  SatALabelEci  { get; private set; } = [];
    public double[]  SatBLabelEci  { get; private set; } = [];
    public string    CaLabel        { get; private set; } = string.Empty;
    public string    SatLabelA      { get; private set; } = string.Empty;
    public string    SatLabelB      { get; private set; } = string.Empty;
    public string    OrbitStartIsoA { get; private set; } = string.Empty;
    public string    OrbitStartIsoB { get; private set; } = string.Empty;
    public string    ClockStartIso  { get; private set; } = string.Empty;
    public string    ClockStopIso   { get; private set; } = string.Empty;
    public string    CaTimeIso      { get; private set; } = string.Empty;

    /// <summary>Raised after plot data is ready so the view can refresh its WpfPlot controls.</summary>
    public event EventHandler? PlotsReady;

    /// <summary>Raised when the user clicks the back button; shell wires this to restore Results.</summary>
    public event Action? NavigateBack;

    [RelayCommand] private void GoBack() => NavigateBack?.Invoke();

    private MeanElements? _elemA;
    private MeanElements? _elemB;

    public EventDetailViewModel(IDbContextFactory<LoiterScanDbContext> factory, IPropagator propagator)
    {
        _factory    = factory;
        _propagator = propagator;
    }

    public async Task LoadAsync(long eventId)
    {
        HasData = false;
        RicR = RicI = RicC = null;
        RangeTimeOADate = RangeKm = null;
        RicLabelPoints = [];
        OrbitEcefA = OrbitEcefB = null;
        OrbitEciA  = OrbitEciB  = null;
        SatALabelEcef = SatBLabelEcef = [];
        SatALabelEci  = SatBLabelEci  = [];
        CaLabel = string.Empty;
        SatLabelA = SatLabelB = string.Empty;
        OrbitStartIsoA = OrbitStartIsoB = ClockStartIso = ClockStopIso = CaTimeIso = string.Empty;

        await using var db = _factory.CreateDbContext();
        var ev = await db.LoiteringEvents.FindAsync(eventId);
        if (ev is null) return;
        Event = ev;

        MinRangeKm       = ev.MinRangeKm;
        CloseApproach    = ev.CloseApproachUtc.ToString("yyyy-MM-dd HH:mm") + " UTC";
        DurationMinutes  = ev.DurationMinutes;
        Confidence       = ev.Confidence;

        var catA = await db.CatalogObjects.FindAsync(ev.NoradIdA);
        var catB = await db.CatalogObjects.FindAsync(ev.NoradIdB);

        ObjectALabel = catA is not null ? $"{ev.NoradIdA} {catA.Name}" : ev.NoradIdA.ToString();
        ObjectBLabel = catB is not null ? $"{ev.NoradIdB} {catB.Name}" : ev.NoradIdB.ToString();
        SatLabelA = string.IsNullOrEmpty(catA?.Name) ? ev.NoradIdA.ToString() : catA!.Name;
        SatLabelB = string.IsNullOrEmpty(catB?.Name) ? ev.NoradIdB.ToString() : catB!.Name;

        if (catA is null || catB is null)
        {
            PlotsReady?.Invoke(this, EventArgs.Empty);
            return;
        }

        _elemA = EntityToElements(catA);
        _elemB = EntityToElements(catB);

        await Task.Run(() => ComputePlotData(ev));

        HasData = true;
        PlotsReady?.Invoke(this, EventArgs.Empty);
    }

    private void ComputePlotData(LoiteringEventEntity ev)
    {
        // Propagate at 1-minute steps over loiter window ±30 min
        var t0   = ev.LoiterStartUtc - TimeSpan.FromMinutes(30);
        var t1   = ev.LoiterEndUtc   + TimeSpan.FromMinutes(30);
        int steps = (int)Math.Ceiling((t1 - t0).TotalMinutes) + 1;

        var ricR = new List<double>(steps);
        var ricI = new List<double>(steps);
        var ricC = new List<double>(steps);
        var tMin = new List<double>(steps);
        var rng  = new List<double>(steps);

        for (int i = 0; i < steps; i++)
        {
            var t = t0 + TimeSpan.FromMinutes(i);
            var sA = _propagator!.Propagate(_elemA!, t);
            var sB = _propagator!.Propagate(_elemB!, t);

            var (r, ic, c) = EciToRic(sA, sB);
            ricR.Add(r);
            ricI.Add(ic);
            ricC.Add(c);

            double dx = sB.X - sA.X, dy = sB.Y - sA.Y, dz = sB.Z - sA.Z;
            rng.Add(Math.Sqrt(dx * dx + dy * dy + dz * dz));
            tMin.Add(t.ToOADate());
        }

        RicR = [.. ricR];
        RicI = [.. ricI];
        RicC = [.. ricC];
        RangeTimeOADate = [.. tMin];
        RangeKm         = [.. rng];
        LoiterStartOA   = ev.LoiterStartUtc.ToOADate();
        LoiterEndOA     = ev.LoiterEndUtc.ToOADate();

        int caIdx = Math.Clamp((int)Math.Round((ev.CloseApproachUtc - t0).TotalMinutes), 0, steps - 1);
        RicLabelPoints = [
            (ricR[0],          ricI[0],          t0),
            (ricR[caIdx],      ricI[caIdx],      t0 + TimeSpan.FromMinutes(caIdx)),
            (ricR[steps - 1],  ricI[steps - 1],  t0 + TimeSpan.FromMinutes(steps - 1)),
        ];

        // Orbital paths for Cesium — 1-min ephemeris over the loiter event window
        var caTime = ev.CloseApproachUtc;
        var (ecefA, eciA, startIsoA, stopIsoA) = ComputeOrbitData(_elemA!, ev.LoiterStartUtc, ev.LoiterEndUtc);
        var (ecefB, eciB, startIsoB, _)         = ComputeOrbitData(_elemB!, ev.LoiterStartUtc, ev.LoiterEndUtc);
        OrbitEcefA     = ecefA;  OrbitEciA      = eciA;
        OrbitEcefB     = ecefB;  OrbitEciB      = eciB;
        OrbitStartIsoA = startIsoA;
        OrbitStartIsoB = startIsoB;
        ClockStartIso  = UtcIso(ev.LoiterStartUtc);
        ClockStopIso   = UtcIso(ev.LoiterEndUtc);

        double gmstCA = ComputeGmstRad(caTime);
        var sAca = _propagator!.Propagate(_elemA!, caTime);
        var sBca = _propagator!.Propagate(_elemB!, caTime);
        var (lax, lay, laz) = TemeToEcef(sAca.X, sAca.Y, sAca.Z, gmstCA);
        var (lbx, lby, lbz) = TemeToEcef(sBca.X, sBca.Y, sBca.Z, gmstCA);
        SatALabelEcef = [lax, lay, laz];
        SatBLabelEcef = [lbx, lby, lbz];
        SatALabelEci  = [sAca.X * 1000.0, sAca.Y * 1000.0, sAca.Z * 1000.0];
        SatBLabelEci  = [sBca.X * 1000.0, sBca.Y * 1000.0, sBca.Z * 1000.0];
        CaLabel   = $"Closest Approach\n{ev.CloseApproachUtc:yyyy-MM-dd HH:mm} UTC\n{ev.MinRangeKm:F2} km";
        CaTimeIso = UtcIso(ev.CloseApproachUtc);
    }

    // Decomposes relative position into Radial / In-track / Cross-track (RIC) frame.
    private static (double R, double I, double C) EciToRic(OrbitState primary, OrbitState secondary)
    {
        // Radial unit vector (along primary position)
        double rx = primary.X, ry = primary.Y, rz = primary.Z;
        double rMag = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        rx /= rMag; ry /= rMag; rz /= rMag;

        // Cross-track unit vector (orbit normal h = r × v)
        double hx = primary.Y * primary.Vz - primary.Z * primary.Vy;
        double hy = primary.Z * primary.Vx - primary.X * primary.Vz;
        double hz = primary.X * primary.Vy - primary.Y * primary.Vx;
        double hMag = Math.Sqrt(hx * hx + hy * hy + hz * hz);
        hx /= hMag; hy /= hMag; hz /= hMag;

        // In-track unit vector (C × R, completing right-hand system)
        double ix = hy * rz - hz * ry;
        double iy = hz * rx - hx * rz;
        double iz = hx * ry - hy * rx;

        // Relative position
        double dx = secondary.X - primary.X;
        double dy = secondary.Y - primary.Y;
        double dz = secondary.Z - primary.Z;

        return (
            R: dx * rx + dy * ry + dz * rz,
            I: dx * ix + dy * iy + dz * iz,
            C: dx * hx + dy * hy + dz * hz);
    }

    private static MeanElements EntityToElements(CatalogObjectEntity c) => new(
        MeanMotionRevPerDay: c.MeanMotionRevPerDay,
        Eccentricity:        c.Eccentricity,
        InclinationDeg:      c.InclinationDeg,
        RaanDeg:             c.RaanDeg,
        ArgPerigeeDeg:       c.ArgPerigeeDeg,
        MeanAnomalyDeg:      c.MeanAnomalyDeg,
        BStar:               c.BStar,
        EpochUtc:            c.EpochUtc);

    // Propagates over [windowStart, windowEnd] at 1-min intervals (capped at 1500 steps).
    // Single pass produces both ECEF (GMST-rotated) and raw ECI (TEME) positions in metres,
    // plus ISO-8601 start/stop timestamps for the Cesium SampledPositionProperty epoch.
    private (double[] Ecef, double[] Eci, string StartIso, string StopIso) ComputeOrbitData(
        MeanElements elem, DateTime windowStart, DateTime windowEnd)
    {
        int steps = Math.Max(2, Math.Min((int)Math.Ceiling((windowEnd - windowStart).TotalMinutes) + 1, 1500));

        var ecef = new double[steps * 3];
        var eci  = new double[steps * 3];
        for (int i = 0; i < steps; i++)
        {
            var    t           = windowStart + TimeSpan.FromMinutes(i);
            var    state       = _propagator!.Propagate(elem, t);
            double gmst        = ComputeGmstRad(t);
            var   (ex, ey, ez) = TemeToEcef(state.X, state.Y, state.Z, gmst);
            ecef[i * 3]     = ex;
            ecef[i * 3 + 1] = ey;
            ecef[i * 3 + 2] = ez;
            eci[i * 3]      = state.X * 1000.0;
            eci[i * 3 + 1]  = state.Y * 1000.0;
            eci[i * 3 + 2]  = state.Z * 1000.0;
        }
        var actualEnd = windowStart + TimeSpan.FromMinutes(steps - 1);
        return (ecef, eci, UtcIso(windowStart), UtcIso(actualEnd));
    }

    // Forces UTC kind before ToString("O") so the output always carries a Z suffix.
    // EF Core + SQLite returns DateTimeKind.Unspecified; without Z, Cesium treats the
    // timestamp as local time and offsets the clock by the machine's UTC offset.
    private static string UtcIso(DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O");

    // GMST (radians) via the IAU 1982 formula; accuracy ≈ 0.1 s over ±100 yr — adequate for display.
    private static double ComputeGmstRad(DateTime utc)
    {
        double ut1 = utc.TimeOfDay.TotalDays;
        double jd  = 367 * utc.Year
            - Math.Floor(7 * (utc.Year + Math.Floor((utc.Month + 9.0) / 12)) / 4)
            + Math.Floor(275 * utc.Month / 9.0)
            + utc.Day + 1721013.5 + ut1;

        double t   = (jd - 2451545.0) / 36525.0;
        double deg = 280.46061837
            + 360.98564736629 * (jd - 2451545.0)
            + 0.000387933 * t * t
            - t * t * t / 38710000.0;

        return ((deg % 360.0 + 360.0) % 360.0) * (Math.PI / 180.0);
    }

    // Rotates TEME position (km) into ECEF (metres) via GMST angle.
    private static (double X, double Y, double Z) TemeToEcef(double x, double y, double z, double gmstRad)
    {
        double c = Math.Cos(gmstRad);
        double s = Math.Sin(gmstRad);
        return (
            (x * c + y * s) * 1000.0,
            (-x * s + y * c) * 1000.0,
            z * 1000.0);
    }
}
