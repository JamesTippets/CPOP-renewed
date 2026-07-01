using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LoiterScan.Analytics;
using LoiterScan.App.Services;
using LoiterScan.Core.Models;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Analytics view: recurring pairs summary with an interactive distribution chart.
/// Clicking a column header switches which column is visualised; clicking a chart bar
/// highlights the corresponding row in the DataGrid.
/// </summary>
public sealed partial class AnalyticsViewModel : ObservableObject
{
    private readonly RunService              _runSvc;
    private readonly RecurringPairsAnalyzer  _recurringAnalyzer;

    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private int    _totalRuns;
    [ObservableProperty] private int    _totalEvents;
    [ObservableProperty] private int    _uniquePairs;

    public ObservableCollection<RecurringPairSummary> RecurringPairs { get; } = [];

    // Parallel arrays for the distribution bar chart.
    // Index i in each array corresponds to RecurringPairs[i] at the time the chart was built.
    public double[]?               DistributionValues         { get; private set; }
    public RecurringPairSummary[]? DistributionItems          { get; private set; }
    public string[]?               DistributionLabels         { get; private set; }
    /// <summary>Non-null for categorical histograms (e.g. Regime); ordered bin labels indexed by DistributionValues[i].</summary>
    public string[]?               DistributionCategoryLabels { get; private set; }
    public string                  SelectedColumn             { get; private set; } = "Min Range (km)";

    public event EventHandler? DistributionChartReady;

    public AnalyticsViewModel(RunService runSvc, RecurringPairsAnalyzer recurringAnalyzer)
    {
        _runSvc            = runSvc;
        _recurringAnalyzer = recurringAnalyzer;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var records = await _runSvc.GetAllRunRecordsAsync();
            TotalRuns   = records.Count;
            TotalEvents = records.Sum(r => r.Events.Count);

            var recurring = _recurringAnalyzer.Analyze(records)
                .Where(p => p.AllTimeMinRangeKm > 0.0)
                .ToList();

            var allIds    = recurring.SelectMany(p => new[] { p.Pair.Low, p.Pair.High }).Distinct();
            var regimeMap = await _runSvc.GetRegimeMapAsync(allIds);
            var enriched  = recurring
                .Select(p => p with {
                    RegimeA = regimeMap.GetValueOrDefault(p.Pair.Low,  OrbitRegime.Unknown),
                    RegimeB = regimeMap.GetValueOrDefault(p.Pair.High, OrbitRegime.Unknown),
                })
                .ToList();

            UniquePairs = enriched.Count;
            RecurringPairs.Clear();
            foreach (var p in enriched) RecurringPairs.Add(p);

            BuildDistributionChart("Min Range (km)");
            DistributionChartReady?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Switch the distribution chart to a different column. Called from the view on column header click.</summary>
    public void SelectColumn(string column)
    {
        if (column is "NORAD-A" or "NORAD-B" or "Trend") return;
        BuildDistributionChart(column);
        DistributionChartReady?.Invoke(this, EventArgs.Empty);
    }

    private void BuildDistributionChart(string column)
    {
        if (RecurringPairs.Count == 0)
        {
            DistributionValues         = null;
            DistributionItems          = null;
            DistributionLabels         = null;
            DistributionCategoryLabels = null;
            SelectedColumn             = column;
            return;
        }

        var items = RecurringPairs.ToArray();
        DistributionCategoryLabels = null;

        double[] values;
        switch (column)
        {
            case "Runs":
                values = items.Select(p => (double)p.RecurrenceCount).ToArray();
                break;
            case "Episodes":
                values = items.Select(p => (double)p.EpisodeCount).ToArray();
                break;
            case "First Seen":
                var firstBase = items.Min(p => p.FirstSeen.Date);
                values = items.Select(p => (p.FirstSeen.Date - firstBase).TotalDays).ToArray();
                break;
            case "Last Seen":
                var lastBase = items.Min(p => p.LastSeen.Date);
                values = items.Select(p => (p.LastSeen.Date - lastBase).TotalDays).ToArray();
                break;
            case "Regime":
            {
                var combos     = new List<string>();
                var comboIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var item in items)
                {
                    string label = item.RegimeLabel;
                    if (!comboIndex.ContainsKey(label))
                    {
                        comboIndex[label] = combos.Count;
                        combos.Add(label);
                    }
                }
                values = items.Select(p => (double)comboIndex[p.RegimeLabel]).ToArray();
                DistributionCategoryLabels = combos.ToArray();
                break;
            }
            default:
                values = items.Select(p => p.AllTimeMinRangeKm).ToArray();
                column = "Min Range (km)";
                break;
        }

        DistributionValues = values;
        DistributionItems  = items;
        DistributionLabels = items.Select(p => $"{p.Pair.Low}/{p.Pair.High}").ToArray();
        SelectedColumn     = column;
    }
}
