using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LoiterScan.Analytics;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App.Views;

public partial class AnalyticsView : UserControl
{
    private AnalyticsViewModel? _vm;

    private const double MinRangeBinKm  = 0.5;
    private const double EpisodeBinSize = 1.0;

    // Pastel palette cycled over histogram bins
    private static readonly ScottPlot.Color[] PastelPalette =
    [
        new ScottPlot.Color(255, 179, 186), // pastel red
        new ScottPlot.Color(255, 218, 185), // pastel peach
        new ScottPlot.Color(255, 255, 186), // pastel yellow
        new ScottPlot.Color(186, 255, 201), // pastel green
        new ScottPlot.Color(186, 225, 255), // pastel blue
        new ScottPlot.Color(218, 186, 255), // pastel purple
        new ScottPlot.Color(255, 186, 240), // pastel pink
        new ScottPlot.Color(186, 255, 255), // pastel cyan
    ];

    public AnalyticsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DistributionPlot.MouseLeftButtonDown += OnPlotClick;
        // Fallback: if data was already loaded before the view rendered, build the chart now.
        Loaded += (_, _) => { if (_vm?.DistributionValues is { Length: > 0 }) BuildDistributionChart(); };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.DistributionChartReady -= OnDistributionChartReady;
        _vm = e.NewValue as AnalyticsViewModel;
        if (_vm is not null)
        {
            _vm.DistributionChartReady += OnDistributionChartReady;
            if (_vm.DistributionValues is { Length: > 0 })
                Dispatcher.BeginInvoke(BuildDistributionChart);
        }
    }

    private void OnDistributionChartReady(object? sender, EventArgs e)
        => Dispatcher.Invoke(BuildDistributionChart);

    private void BuildDistributionChart()
    {
        DistributionPlot.Plot.Clear();
        DistributionPlot.Plot.Axes.Rules.Clear();

        var values = _vm?.DistributionValues;
        var labels = _vm?.DistributionLabels;
        if (values is not { Length: > 0 } || labels is null)
        {
            DistributionPlot.Refresh();
            return;
        }

        int    n   = values.Length;
        string col = _vm!.SelectedColumn;

        double yMax;

        if (col == "Min Range (km)")
        {
            // Histogram: bin pairs into 0.5 km buckets; X = range, Y = number of pairs
            int numBins = (int)Math.Ceiling(values.Max() / MinRangeBinKm) + 1;
            var binCounts = new double[numBins];
            foreach (double v in values)
            {
                int b = (int)Math.Floor(v / MinRangeBinKm);
                if ((uint)b < (uint)numBins) binCounts[b]++;
            }

            // Bars centred in each bucket; distinct pastel colour per bin
            var barItems = new List<ScottPlot.Bar>(numBins);
            for (int b = 0; b < numBins; b++)
            {
                if (binCounts[b] > 0)
                    barItems.Add(new ScottPlot.Bar
                    {
                        Position  = b * MinRangeBinKm + MinRangeBinKm / 2.0,
                        Value     = binCounts[b],
                        Size      = MinRangeBinKm,
                        FillColor = PastelPalette[b % PastelPalette.Length],
                    });
            }

            DistributionPlot.Plot.Add.Bars(barItems);

            // Tick at every bin boundary (0.0, 0.5, 1.0, …) so each bar's range is legible
            var xGen = new ScottPlot.TickGenerators.NumericManual();
            for (int b = 0; b <= numBins; b++)
                xGen.AddMajor(b * MinRangeBinKm, $"{b * MinRangeBinKm:F1}");
            DistributionPlot.Plot.Axes.Bottom.TickGenerator = xGen;

            // Match y-axis: horizontal labels, default font size, centred under each tick mark.
            // Rotation and FontSize must be explicitly reset here because the per-pair branch
            // sets them to 90° / 8 pt and Plot.Clear() does not restore axis styles.
            var xStyle = DistributionPlot.Plot.Axes.Bottom.TickLabelStyle;
            xStyle.Rotation  = 0f;
            xStyle.FontSize  = DistributionPlot.Plot.Axes.Left.TickLabelStyle.FontSize;
            xStyle.Alignment = ScottPlot.Alignment.UpperCenter;

            DistributionPlot.Plot.XLabel("Min Range (km)");
            DistributionPlot.Plot.YLabel("Number of Pairs");

            yMax = binCounts.Max() is > 0 ? binCounts.Max() * 1.1 : 1.0;
            var histLimits = new ScottPlot.AxisLimits(0, numBins * MinRangeBinKm, 0, yMax);
            DistributionPlot.Plot.Axes.SetLimits(histLimits);
            DistributionPlot.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.MaximumBoundary(
                DistributionPlot.Plot.Axes.Bottom, DistributionPlot.Plot.Axes.Left, histLimits));
        }
        else if (col == "Episodes")
        {
            // Histogram: one bin per episode count (bin size = 1); X = episode count, Y = number of pairs
            int maxEpisodes = (int)values.Max();
            int numBins     = maxEpisodes + 1;          // bins 0…maxEpisodes; bin 0 is always empty
            var binCounts   = new double[numBins];
            foreach (double v in values)
            {
                int b = (int)Math.Floor(v / EpisodeBinSize);
                if ((uint)b < (uint)numBins) binCounts[b]++;
            }

            // Bars centred in each bucket — same formula as Min Range
            var barItems = new List<ScottPlot.Bar>(numBins);
            for (int b = 0; b < numBins; b++)
            {
                if (binCounts[b] > 0)
                    barItems.Add(new ScottPlot.Bar
                    {
                        Position  = b * EpisodeBinSize + EpisodeBinSize / 2.0,
                        Value     = binCounts[b],
                        Size      = EpisodeBinSize,
                        FillColor = PastelPalette[b % PastelPalette.Length],
                    });
            }

            DistributionPlot.Plot.Add.Bars(barItems);

            // Tick at every bin boundary (0, 1, 2, …) with integer labels
            var xGen = new ScottPlot.TickGenerators.NumericManual();
            for (int b = 0; b <= numBins; b++)
                xGen.AddMajor(b * EpisodeBinSize, (b * EpisodeBinSize).ToString("F0"));
            DistributionPlot.Plot.Axes.Bottom.TickGenerator = xGen;

            // Horizontal labels matching y-axis font size (same reset as Min Range branch)
            var xStyle = DistributionPlot.Plot.Axes.Bottom.TickLabelStyle;
            xStyle.Rotation  = 0f;
            xStyle.FontSize  = DistributionPlot.Plot.Axes.Left.TickLabelStyle.FontSize;
            xStyle.Alignment = ScottPlot.Alignment.UpperCenter;

            DistributionPlot.Plot.XLabel("Episode Count");
            DistributionPlot.Plot.YLabel("Number of Pairs");

            yMax = binCounts.Max() is > 0 ? binCounts.Max() * 1.1 : 1.0;
            var epLimits = new ScottPlot.AxisLimits(0, numBins * EpisodeBinSize, 0, yMax);
            DistributionPlot.Plot.Axes.SetLimits(epLimits);
            DistributionPlot.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.MaximumBoundary(
                DistributionPlot.Plot.Axes.Bottom, DistributionPlot.Plot.Axes.Left, epLimits));
        }
        else if (col == "Regime")
        {
            var catLabels = _vm!.DistributionCategoryLabels;
            if (catLabels is not { Length: > 0 }) { DistributionPlot.Refresh(); return; }

            int numBins   = catLabels.Length;
            var binCounts = new double[numBins];
            foreach (double v in values) binCounts[(int)Math.Round(v)]++;

            var barItems = new List<ScottPlot.Bar>(numBins);
            for (int b = 0; b < numBins; b++)
            {
                if (binCounts[b] > 0)
                    barItems.Add(new ScottPlot.Bar
                    {
                        Position  = b,
                        Value     = binCounts[b],
                        Size      = 0.8,
                        FillColor = PastelPalette[b % PastelPalette.Length],
                    });
            }

            DistributionPlot.Plot.Add.Bars(barItems);

            var xGen = new ScottPlot.TickGenerators.NumericManual();
            for (int b = 0; b < numBins; b++)
                xGen.AddMajor(b, catLabels[b]);
            DistributionPlot.Plot.Axes.Bottom.TickGenerator = xGen;

            var xStyle = DistributionPlot.Plot.Axes.Bottom.TickLabelStyle;
            xStyle.Rotation  = 0f;
            xStyle.FontSize  = DistributionPlot.Plot.Axes.Left.TickLabelStyle.FontSize;
            xStyle.Alignment = ScottPlot.Alignment.UpperCenter;

            DistributionPlot.Plot.XLabel("Orbital Regime");
            DistributionPlot.Plot.YLabel("Number of Pairs");

            yMax = binCounts.Max() is > 0 ? binCounts.Max() * 1.1 : 1.0;
            var regimeLimits = new ScottPlot.AxisLimits(-0.5, numBins - 0.5, 0, yMax);
            DistributionPlot.Plot.Axes.SetLimits(regimeLimits);
            DistributionPlot.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.MaximumBoundary(
                DistributionPlot.Plot.Axes.Bottom, DistributionPlot.Plot.Axes.Left, regimeLimits));
        }
        else
        {
            // Per-pair bar chart: one bar per recurring pair
            var barItems = Enumerable.Range(0, n)
                .Select(i => new ScottPlot.Bar { Position = i, Value = values[i] })
                .ToList();
            DistributionPlot.Plot.Add.Bars(barItems);

            // X-axis tick labels — pair IDs for most columns; none for date columns
            if (col is "First Seen" or "Last Seen")
            {
                DistributionPlot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual();
            }
            else
            {
                var gen  = new ScottPlot.TickGenerators.NumericManual();
                int step = n switch { > 50 => 10, > 20 => 5, > 10 => 2, _ => 1 };
                for (int i = 0; i < n; i += step)
                    gen.AddMajor(i, labels[i]);
                DistributionPlot.Plot.Axes.Bottom.TickGenerator = gen;
            }
            DistributionPlot.Plot.Axes.Bottom.TickLabelStyle.FontSize = 8f;
            DistributionPlot.Plot.Axes.Bottom.TickLabelStyle.Rotation = 90f;

            string yLabel = col switch
            {
                "Runs"       => "Number of Runs",
                "First Seen" => "Days Since Earliest",
                "Last Seen"  => "Days Since Earliest",
                _            => col,
            };
            DistributionPlot.Plot.YLabel(yLabel);

            if (col == "Runs")
                DistributionPlot.Plot.XLabel("SatID A / SatID B");

            yMax = values.Max() is > 0 ? values.Max() * 1.1 : 1.0;
            var pairLimits = new ScottPlot.AxisLimits(-0.5, n - 0.5, 0, yMax);
            DistributionPlot.Plot.Axes.SetLimits(pairLimits);
            DistributionPlot.Plot.Axes.Rules.Add(new ScottPlot.AxisRules.MaximumBoundary(
                DistributionPlot.Plot.Axes.Bottom, DistributionPlot.Plot.Axes.Left, pairLimits));
        }

        DistributionPlot.Plot.Title($"Distribution: {col}");
        DistributionPlot.Refresh();
    }

    private void OnPlotClick(object sender, MouseButtonEventArgs e)
    {
        var items  = _vm?.DistributionItems;
        var values = _vm?.DistributionValues;
        if (items is not { Length: > 0 } || values is null) return;

        var pos   = e.GetPosition(DistributionPlot);
        var coord = DistributionPlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);

        if (_vm!.SelectedColumn is "Min Range (km)" or "Episodes")
        {
            // Histogram click: select every pair that falls in the clicked bin
            double binSize    = _vm.SelectedColumn == "Episodes" ? EpisodeBinSize : MinRangeBinKm;
            int    clickedBin = (int)Math.Floor(coord.X / binSize);
            PairsGrid.UnselectAll();
            RecurringPairSummary? firstMatch = null;
            for (int i = 0; i < values.Length; i++)
            {
                if ((int)Math.Floor(values[i] / binSize) == clickedBin)
                {
                    PairsGrid.SelectedItems.Add(items[i]);
                    firstMatch ??= items[i];
                }
            }
            if (firstMatch is null) return;
            PairsGrid.ScrollIntoView(firstMatch);
        }
        else if (_vm.SelectedColumn == "Regime")
        {
            // Categorical histogram click: bin index stored as integer in DistributionValues
            int clickedBin = (int)Math.Round(coord.X);
            PairsGrid.UnselectAll();
            RecurringPairSummary? firstMatch = null;
            for (int i = 0; i < values.Length; i++)
            {
                if ((int)Math.Round(values[i]) == clickedBin)
                {
                    PairsGrid.SelectedItems.Add(items[i]);
                    firstMatch ??= items[i];
                }
            }
            if (firstMatch is null) return;
            PairsGrid.ScrollIntoView(firstMatch);
        }
        else
        {
            int idx = Math.Clamp((int)Math.Round(coord.X), 0, items.Length - 1);
            PairsGrid.SelectedItem = items[idx];
            PairsGrid.ScrollIntoView(items[idx]);
        }

        PairsGrid.Focus();
    }

    private void OnDataGridSorting(object sender, DataGridSortingEventArgs e)
    {
        if (_vm is null) return;
        var header = e.Column.Header as string;
        if (header is null or "NORAD-A" or "NORAD-B" or "Trend") return;
        _vm.SelectColumn(header);
    }
}
