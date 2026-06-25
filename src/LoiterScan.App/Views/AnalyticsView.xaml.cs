using System.Windows;
using System.Windows.Controls;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App.Views;

public partial class AnalyticsView : UserControl
{
    private AnalyticsViewModel? _vm;

    public AnalyticsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.TrendChartReady -= OnTrendChartReady;
        _vm = e.NewValue as AnalyticsViewModel;
        if (_vm is not null) _vm.TrendChartReady += OnTrendChartReady;
    }

    private void OnTrendChartReady(object? sender, EventArgs e) => Dispatcher.Invoke(BuildTrendChart);

    private void BuildTrendChart()
    {
        TrendPlot.Plot.Clear();

        if (_vm?.TrendRunIndices is not null)
        {
            var sc1 = TrendPlot.Plot.Add.Scatter(_vm.TrendRunIndices, _vm.TrendNewPairs!);
            sc1.LegendText = "New pairs";

            var sc2 = TrendPlot.Plot.Add.Scatter(_vm.TrendRunIndices, _vm.TrendRecurPairs!);
            sc2.LegendText = "Recurring pairs";

            TrendPlot.Plot.ShowLegend();
            TrendPlot.Plot.XLabel("Run #");
            TrendPlot.Plot.YLabel("Pairs");
        }

        TrendPlot.Refresh();
    }
}
