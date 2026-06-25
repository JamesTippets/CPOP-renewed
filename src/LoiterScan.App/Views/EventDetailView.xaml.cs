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
        if (_vm.HasData && _vm.RicR is not null)
        {
            RicPlot.Plot.Add.Scatter(_vm.RicR, _vm.RicI!);
            RicPlot.Plot.XLabel("Radial (km)");
            RicPlot.Plot.YLabel("In-track (km)");
        }
        RicPlot.Refresh();

        RangePlot.Plot.Clear();
        if (_vm.HasData && _vm.RangeKm is not null)
        {
            RangePlot.Plot.Add.Scatter(_vm.RangeTimeMinutes!, _vm.RangeKm);

            var vStart  = RangePlot.Plot.Add.VerticalLine(_vm.LoiterStartMin);
            vStart.Color = ScottPlot.Colors.Green;

            var vEnd    = RangePlot.Plot.Add.VerticalLine(_vm.LoiterEndMin);
            vEnd.Color   = ScottPlot.Colors.Red;

            var hThresh  = RangePlot.Plot.Add.HorizontalLine(_vm.ThresholdKm);
            hThresh.Color = ScottPlot.Colors.Orange;

            RangePlot.Plot.XLabel("Time (min from window start)");
            RangePlot.Plot.YLabel("Range (km)");
        }
        RangePlot.Refresh();
    }
}
