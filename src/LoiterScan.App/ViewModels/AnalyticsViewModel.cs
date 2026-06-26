using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LoiterScan.Analytics;
using LoiterScan.App.Services;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Analytics view: recurring pairs summary and config-scoped event-count trends (spec §8).
/// Trend chart data is exposed as arrays; the view code-behind renders the ScottPlot.
/// </summary>
public sealed partial class AnalyticsViewModel : ObservableObject
{
    private readonly RunService              _runSvc;
    private readonly RecurringPairsAnalyzer  _recurringAnalyzer;
    private readonly TrendAnalyzer           _trendAnalyzer;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private int    _totalRuns;
    [ObservableProperty] private int    _totalEvents;
    [ObservableProperty] private int    _uniquePairs;

    public ObservableCollection<RecurringPairSummary> RecurringPairs { get; } = [];
    public ObservableCollection<ConfigScopedTrend>    Trends          { get; } = [];

    // Trend chart data (first config-scope, if any)
    public double[]? TrendRunIndices  { get; private set; }
    public double[]? TrendNewPairs     { get; private set; }
    public double[]? TrendRecurPairs   { get; private set; }
    public string[]? TrendRunLabels    { get; private set; }

    /// <summary>Raised when trend chart data is ready for the view to refresh.</summary>
    public event EventHandler? TrendChartReady;

    public AnalyticsViewModel(
        RunService              runSvc,
        RecurringPairsAnalyzer  recurringAnalyzer,
        TrendAnalyzer           trendAnalyzer)
    {
        _runSvc            = runSvc;
        _recurringAnalyzer = recurringAnalyzer;
        _trendAnalyzer     = trendAnalyzer;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var records = await _runSvc.GetAllRunRecordsAsync();
            TotalRuns   = records.Count;
            TotalEvents = records.Sum(r => r.Events.Count);

            var recurring = _recurringAnalyzer.Analyze(records);
            UniquePairs   = recurring.Count;

            RecurringPairs.Clear();
            foreach (var p in recurring) RecurringPairs.Add(p);

            var trends = _trendAnalyzer.Analyze(records);
            Trends.Clear();
            foreach (var t in trends) Trends.Add(t);

            BuildTrendChartData(trends);
            TrendChartReady?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildTrendChartData(IReadOnlyList<ConfigScopedTrend> trends)
    {
        // Use the largest config-scope for the primary trend chart
        var scope = trends.MaxBy(t => t.Points.Count);
        if (scope is null || scope.Points.Count == 0)
        {
            TrendRunIndices = null;
            return;
        }

        int n = scope.Points.Count;
        TrendRunIndices = Enumerable.Range(1, n).Select(i => (double)i).ToArray();
        TrendNewPairs   = scope.Points.Select(p => (double)p.NewPairs).ToArray();
        TrendRecurPairs = scope.Points.Select(p => (double)p.RecurringPairs).ToArray();
        TrendRunLabels  = scope.Points.Select(p => p.StartedAt.ToLocalTime().ToString("MM-dd")).ToArray();
    }
}
