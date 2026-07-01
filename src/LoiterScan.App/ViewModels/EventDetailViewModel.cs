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
    [ObservableProperty] private string _objectAType  = "—";
    [ObservableProperty] private string _objectBType  = "—";
    [ObservableProperty] private string _objectASize  = "—";
    [ObservableProperty] private string _objectBSize  = "—";
    [ObservableProperty] private string _objectATle   = "—";
    [ObservableProperty] private string _objectBTle   = "—";
    [ObservableProperty] private double _minRangeKm;
    [ObservableProperty] private double _minRicR;
    [ObservableProperty] private double _minRicI;
    [ObservableProperty] private double _minRicC;
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
    private double _caRicR, _caRicI, _caRicC;

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
        ObjectAType = ObjectBType = ObjectASize = ObjectBSize = ObjectATle = ObjectBTle = "—";
        MinRicR = MinRicI = MinRicC = 0;

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
        ObjectAType  = catA?.ObjectType ?? "—";
        ObjectBType  = catB?.ObjectType ?? "—";
        ObjectASize  = FormatRegime(catA?.Regime);
        ObjectBSize  = FormatRegime(catB?.Regime);
        ObjectATle   = catA is not null ? FormatTle(catA) : "—";
        ObjectBTle   = catB is not null ? FormatTle(catB) : "—";
        SatLabelA = string.IsNullOrEmpty(catA?.Name) ? ev.NoradIdA.ToString() : catA!.Name;
        SatLabelB = string.IsNullOrEmpty(catB?.Name) ? ev.NoradIdB.ToString() : catB!.Name;

        if (catA is null || catB is null)
        {
            PlotsReady?.Invoke(this, EventArgs.Empty);
            return;
        }

        _elemA = catA.ToMeanElements();
        _elemB = catB.ToMeanElements();

        await Task.Run(() => ComputePlotData(ev));

        MinRicR = _caRicR;
        MinRicI = _caRicI;
        MinRicC = _caRicC;
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
        _caRicR = ricR[caIdx];
        _caRicI = ricI[caIdx];
        _caRicC = ricC[caIdx];
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

    private static (double R, double I, double C) EciToRic(OrbitState primary, OrbitState secondary)
        => RicFrame.EciToRic(primary, secondary);

    private static string FormatRegime(string? regime) =>
        regime is null or "Unknown" ? "—" : regime.ToUpperInvariant();

    private static string FormatTle(CatalogObjectEntity c)
    {
        // Epoch: YYDDD.FFFFFFFF (14 chars)
        var ep = c.EpochUtc;
        string epoch = $"{ep.Year % 100:D2}{ep.DayOfYear:D3}.{ep.TimeOfDay.TotalDays:F8}";

        // Intl designator: strip dashes, take last 8 chars, pad to 8
        string intl = (c.IntlDesignator ?? "").Replace("-", "");
        intl = (intl.Length > 8 ? intl[^8..] : intl).PadRight(8);

        // Ndot/2 as ±.NNNNNNNN (OMM MEAN_MOTION_DOT already stores ndot/2)
        double nd = c.MeanMotionDotRevPerDay;
        string ndotStr = $"{(nd >= 0 ? " " : "-")}.{(long)(Math.Abs(nd) * 1e8):D8}";

        // Nddot/6 and BStar in ±NNNNN±N scientific form (OMM stores the divided values)
        string nddotStr = FormatSciTle(c.MeanMotionDdotRevPerDay);
        string bstarStr = FormatSciTle(c.BStar);

        // Eccentricity: 7-digit integer, no decimal
        string ecc = ((long)(c.Eccentricity * 1e7)).ToString("D7");

        string line1 = $"1 {c.NoradId:D5}U {intl} {epoch} {ndotStr} {nddotStr} {bstarStr} {c.EphemerisType}    0";
        string line2 = $"2 {c.NoradId:D5} {c.InclinationDeg,8:F4} {c.RaanDeg,8:F4} {ecc} {c.ArgPerigeeDeg,8:F4} {c.MeanAnomalyDeg,8:F4} {c.MeanMotionRevPerDay,11:F8}    0";
        return $"{line1}\n{line2}";
    }

    private static string FormatSciTle(double v)
    {
        if (v == 0.0) return " 00000-0";
        char sign  = v >= 0 ? ' ' : '-';
        double abs = Math.Abs(v);
        int exp    = (int)Math.Floor(Math.Log10(abs)) + 1;
        int mant   = (int)Math.Round(abs * Math.Pow(10, 5 - exp));
        if (mant >= 100000) { mant /= 10; exp++; }
        char expSign = exp >= 0 ? '+' : '-';
        return $"{sign}{mant:D5}{expSign}{Math.Abs(exp)}";
    }

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
