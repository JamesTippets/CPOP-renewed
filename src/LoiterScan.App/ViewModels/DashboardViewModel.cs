using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoiterScan.App.Services;
using LoiterScan.Data.Entities;
using LoiterScan.Engine;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Dashboard / Run view: catalog status cards, run trigger, live progress, recent-runs list.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly CatalogStatusService _statusSvc;
    private readonly ConfigService        _configSvc;
    private readonly RunService           _runSvc;
    private readonly DetectionPipeline    _pipeline;
    private readonly CatalogCacheService  _cacheService;

    private CancellationTokenSource? _cts;

    // ── Catalog status ────────────────────────────────────────────────────────
    [ObservableProperty] private int      _totalObjects;
    [ObservableProperty] private int      _staleObjects;
    [ObservableProperty] private int      _decayedObjects;
    [ObservableProperty] private string   _lastRefresh = "—";
    [ObservableProperty] private string   _catalogSource2 = "—";

    // ── Run controls ──────────────────────────────────────────────────────────
    [ObservableProperty] private bool _refreshBeforeRun;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRunCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRunCommand))]
    private bool _isRunning;

    [ObservableProperty] private double _progressPercentage;
    [ObservableProperty] private string _progressMessage  = string.Empty;
    [ObservableProperty] private bool   _isProgressIndeterminate = true;
    [ObservableProperty] private string _statusMessage    = string.Empty;

    // ── Recent runs ───────────────────────────────────────────────────────────
    public ObservableCollection<RunEntity> RecentRuns { get; } = [];

    [ObservableProperty] private RunEntity? _selectedRun;

    partial void OnSelectedRunChanged(RunEntity? value)
    {
        if (value is not null)
            RunSelected?.Invoke(value.RunId);
    }

    /// <summary>Raised when the user selects a run; carries the RunId to navigate to Results.</summary>
    public event Action<long>? RunSelected;

    public DashboardViewModel(
        CatalogStatusService statusSvc,
        ConfigService        configSvc,
        RunService           runSvc,
        DetectionPipeline    pipeline,
        CatalogCacheService  cacheService)
    {
        _statusSvc    = statusSvc;
        _configSvc    = configSvc;
        _runSvc       = runSvc;
        _pipeline     = pipeline;
        _cacheService = cacheService;
    }

    public async Task LoadAsync()
    {
        await RefreshCatalogStatusAsync();
        await RefreshRecentRunsAsync();

        var cfg = await _configSvc.GetParamEntityAsync();
        RefreshBeforeRun = cfg.RefreshBeforeRun;
    }

    [RelayCommand(CanExecute = nameof(CanStartRun))]
    private async Task StartRunAsync()
    {
        _cts                    = new CancellationTokenSource();
        IsRunning               = true;
        StatusMessage           = string.Empty;
        ProgressMessage         = string.Empty;
        ProgressPercentage      = 0;
        IsProgressIndeterminate = true;

        var progress = new Progress<PipelineProgress>(p =>
        {
            ProgressMessage          = p.Message;
            IsProgressIndeterminate  = p.Total == 0;
            ProgressPercentage       = p.Total > 0 ? (double)p.Processed / p.Total * 100.0 : 0;
        });

        long runId = 0;
        try
        {
            var config = await _configSvc.GetConfigAsync();

            // Override refresh-before-run from the per-run toggle
            config = config with
            {
                Acquisition = config.Acquisition with { RefreshBeforeRun = RefreshBeforeRun }
            };

            var run = await _runSvc.StartRunAsync(config);
            runId = run.RunId;

            if (RefreshBeforeRun)
            {
                ProgressMessage = "Refreshing catalog…";
                await _cacheService.EnsureFreshAsync(config.Acquisition, _cts.Token);
            }

            ProgressMessage = "Starting…";

            var runSw  = Stopwatch.StartNew();
            var result = await _pipeline.RunAsync(config, null, progress, _cts.Token);
            runSw.Stop();

            await _runSvc.CompleteRunAsync(runId, result.Events, result.CoarsePairsChecked);
            StatusMessage = $"Completed — {result.Events.Count} event(s) detected in {FormatElapsed(runSw.Elapsed)}.";
        }
        catch (OperationCanceledException)
        {
            if (runId > 0) await _runSvc.FailRunAsync(runId, "cancelled");
            StatusMessage = "Run cancelled.";
        }
        catch (Exception ex)
        {
            if (runId > 0) await _runSvc.FailRunAsync(runId, "failed");
            StatusMessage = $"Run failed: {ex.Message}";
        }
        finally
        {
            IsRunning               = false;
            _cts?.Dispose();
            _cts                    = null;
            ProgressMessage         = string.Empty;
            ProgressPercentage      = 0;
            IsProgressIndeterminate = true;
        }

        await RefreshCatalogStatusAsync();
        await RefreshRecentRunsAsync();
    }

    private bool CanStartRun() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanCancelRun))]
    private void CancelRun() => _cts?.Cancel();

    private bool CanCancelRun() => IsRunning;

    private static string FormatElapsed(TimeSpan t)
    {
        if (t.TotalSeconds < 60)  return $"{t.TotalSeconds:F1}s";
        if (t.TotalMinutes < 60)  return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        return $"{(int)t.TotalHours}h {t.Minutes}m";
    }

    private async Task RefreshCatalogStatusAsync()
    {
        var status     = await _statusSvc.GetStatusAsync();
        TotalObjects   = status.TotalObjects;
        StaleObjects   = status.StaleObjects;
        DecayedObjects = status.DecayedObjects;
        LastRefresh    = status.LastIngestedAt.HasValue
            ? status.LastIngestedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "Never";
        CatalogSource2 = status.Source;
    }

    private async Task RefreshRecentRunsAsync()
    {
        var runs = await _runSvc.GetRecentRunsAsync();
        RecentRuns.Clear();
        foreach (var r in runs) RecentRuns.Add(r);
    }
}
