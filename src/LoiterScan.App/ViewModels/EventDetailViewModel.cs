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
}
