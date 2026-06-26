using System.Windows;
using System.Windows.Controls;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App.Views;

public partial class EventDetailView : UserControl
{
    private EventDetailViewModel? _vm;

    public EventDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PlotsReady -= OnPlotsReady;
        _vm = e.NewValue as EventDetailViewModel;
        if (_vm is not null) _vm.PlotsReady += OnPlotsReady;
    }

    private void OnPlotsReady(object? sender, EventArgs e) => Dispatcher.Invoke(BuildPlots);

    private void BuildPlots()
    {
        if (_vm is null) return;

        RicPlot.Plot.Clear();
        RicPlot.Plot.Axes.Rules.Clear();
        if (_vm.HasData && _vm.RicR is not null)
        {
            // Dark grey background for contrast against the warm gradient colours
            RicPlot.Plot.DataBackground.Color = new ScottPlot.Color(45, 45, 45, 255);
            var ricStyle = RicPlot.Plot.GetStyle();
            ricStyle.AxisColor          = new ScottPlot.Color(200, 200, 200, 255);
            ricStyle.GridMajorLineColor = new ScottPlot.Color(75, 75, 75, 255);
            RicPlot.Plot.SetStyle(ricStyle);

            // Cool-to-hot gradient: draw N-1 individually coloured line segments
            var cmap = new ScottPlot.Colormaps.Turbo();
            int n = _vm.RicR.Length;
            for (int i = 0; i < n - 1; i++)
            {
                double t = (double)i / Math.Max(n - 2, 1);
                var seg = RicPlot.Plot.Add.ScatterLine(
                    new[] { _vm.RicR[i], _vm.RicR[i + 1] },
                    new[] { _vm.RicI![i], _vm.RicI[i + 1] },
                    cmap.GetColor(t));
                seg.LineWidth = 2f;
            }

            // Text-bubble timestamp labels for start, CA, and end.
            // First and last points sit on opposite Y boundaries, so detect which is on top
            // to ensure the label always hangs inward (away from the boundary it's near).
            bool firstAboveLast = _vm.RicLabelPoints.Length >= 3 &&
                                  _vm.RicLabelPoints[0].I > _vm.RicLabelPoints[2].I;

            for (int i = 0; i < _vm.RicLabelPoints.Length; i++)
            {
                var (r, ic, timeUtc) = _vm.RicLabelPoints[i];
                var txt = RicPlot.Plot.Add.Text(timeUtc.ToString("yyyy-MM-dd\nHH:mm UTC"), r, ic);
                txt.LabelFontSize        = 8.5f;
                txt.LabelBackgroundColor = ScottPlot.Colors.White;
                txt.LabelBorderColor     = ScottPlot.Colors.Gray;
                txt.LabelBorderWidth     = 1f;
                txt.LabelBorderRadius    = 4f;
                txt.LabelPadding         = 4f;
                (txt.LabelAlignment, txt.LabelOffsetX, txt.LabelOffsetY) = i switch {
                    0 when firstAboveLast  => (ScottPlot.Alignment.UpperLeft,   8f,   8f),  // first near top  → label hangs down-right
                    0                      => (ScottPlot.Alignment.LowerLeft,   8f,  -8f),  // first near bottom → label hangs up-right
                    1                      => (ScottPlot.Alignment.LowerCenter, 0f, -12f),  // CA in interior → label above
                    _ when !firstAboveLast => (ScottPlot.Alignment.UpperRight, -8f,   8f),  // last near top  → label hangs down-left
                    _                      => (ScottPlot.Alignment.LowerRight, -8f,  -8f),  // last near bottom → label hangs up-left
                };
            }

            // Initial view + maximum zoom-out boundary: X ±1 km beyond farthest radial; Y 1 km beyond first/last in-track
            double maxAbsR = 0;
            foreach (var v in _vm.RicR) if (Math.Abs(v) > maxAbsR) maxAbsR = Math.Abs(v);
            double firstI = _vm.RicI![0];
            double lastI  = _vm.RicI[n - 1];
            var ricLimits = new ScottPlot.AxisLimits(
                -(maxAbsR + 1), maxAbsR + 1,
                Math.Min(firstI, lastI) - 1,
                Math.Max(firstI, lastI) + 1);
            RicPlot.Plot.Axes.SetLimits(ricLimits);
            RicPlot.Plot.Axes.Rules.Add(
                new ScottPlot.AxisRules.MaximumBoundary(
                    RicPlot.Plot.Axes.Bottom, RicPlot.Plot.Axes.Left, ricLimits));

            RicPlot.Plot.XLabel("Radial (km)");
            RicPlot.Plot.YLabel("In-track (km)");
        }
        RicPlot.Refresh();

        RangePlot.Plot.Clear();
        RangePlot.Plot.Axes.Rules.Clear();
        if (_vm.HasData && _vm.RangeKm is not null)
        {
            RangePlot.Plot.Add.Scatter(_vm.RangeTimeMinutes!, _vm.RangeKm);

            var vStart  = RangePlot.Plot.Add.VerticalLine(_vm.LoiterStartMin);
            vStart.Color = ScottPlot.Colors.Green;

            var vEnd    = RangePlot.Plot.Add.VerticalLine(_vm.LoiterEndMin);
            vEnd.Color   = ScottPlot.Colors.Red;

            var hThresh  = RangePlot.Plot.Add.HorizontalLine(_vm.ThresholdKm);
            hThresh.Color = ScottPlot.Colors.Orange;

            RangePlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericFixedInterval(30);

            double[] tMins = _vm.RangeTimeMinutes!;
            var rangeLimits = new ScottPlot.AxisLimits(tMins[0], tMins[^1], 0, 10);
            RangePlot.Plot.Axes.SetLimits(rangeLimits);
            RangePlot.Plot.Axes.Rules.Add(
                new ScottPlot.AxisRules.MaximumBoundary(
                    RangePlot.Plot.Axes.Bottom, RangePlot.Plot.Axes.Left, rangeLimits));

            RangePlot.Plot.XLabel("Time (min from window start)");
            RangePlot.Plot.YLabel("Range (km)");
        }
        RangePlot.Refresh();
    }
}
