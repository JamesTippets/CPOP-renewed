using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoiterScan.App.Services;
using LoiterScan.Data.Entities;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Results view: the loitering event pairs for a selected run, in a virtualized data grid.
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly RunService    _runSvc;
    private readonly ConfigService _configSvc;

    [ObservableProperty] private string _runLabel = "No run selected";
    [ObservableProperty] private int    _eventCount;

    public ObservableCollection<LoiteringEventEntity> Events { get; } = [];

    [ObservableProperty] private LoiteringEventEntity? _selectedEvent;

    partial void OnSelectedEventChanged(LoiteringEventEntity? value) { }

    /// <summary>Raised when the user explicitly left-clicks an event row to navigate to EventDetail.</summary>
    public event Action<long>? EventSelected;

    public void NavigateToSelected()
    {
        if (SelectedEvent is not null)
            EventSelected?.Invoke(SelectedEvent.Id);
    }

    public ResultsViewModel(RunService runSvc, ConfigService configSvc)
    {
        _runSvc    = runSvc;
        _configSvc = configSvc;
    }

    public async Task LoadAsync()
    {
        if (Events.Count > 0) return; // already populated — keep existing selection
        var latest = await _runSvc.GetRecentRunsAsync(1);
        if (latest.Count > 0) await LoadForRunAsync(latest[0].RunId);
    }

    public async Task LoadForRunAsync(long runId)
    {
        var run = (await _runSvc.GetRecentRunsAsync(200)).FirstOrDefault(r => r.RunId == runId);
        RunLabel = run is not null
            ? $"Run {runId} — {run.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm} ({run.Status})"
            : $"Run {runId}";

        var evs = await _runSvc.GetEventsForRunAsync(runId);
        Events.Clear();
        foreach (var e in evs) Events.Add(e);
        EventCount = evs.Count;
    }

    /// <summary>
    /// Excludes the pair in the given event from all future runs and removes matching
    /// events from the current results list immediately.
    /// </summary>
    [RelayCommand]
    private async Task ExcludePairAsync(LoiteringEventEntity? ev)
    {
        if (ev is null) return;

        long lo = Math.Min(ev.NoradIdA, ev.NoradIdB);
        long hi = Math.Max(ev.NoradIdA, ev.NoradIdB);
        await _configSvc.AddExclPairAsync(lo, hi);

        // Remove every event in the current view that belongs to this pair
        var toRemove = Events
            .Where(e => Math.Min(e.NoradIdA, e.NoradIdB) == lo &&
                        Math.Max(e.NoradIdA, e.NoradIdB) == hi)
            .ToList();
        foreach (var e in toRemove)
            Events.Remove(e);

        EventCount = Events.Count;
    }
}
