using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LoiterScan.App.Services;
using LoiterScan.Data.Entities;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Results view: the loitering event pairs for a selected run, in a virtualized data grid.
/// </summary>
public sealed partial class ResultsViewModel : ObservableObject
{
    private readonly RunService _runSvc;

    [ObservableProperty] private string _runLabel = "No run selected";
    [ObservableProperty] private int    _eventCount;

    public ObservableCollection<LoiteringEventEntity> Events { get; } = [];

    [ObservableProperty] private LoiteringEventEntity? _selectedEvent;

    partial void OnSelectedEventChanged(LoiteringEventEntity? value)
    {
        if (value is not null)
            EventSelected?.Invoke(value.Id);
    }

    /// <summary>Raised when the user selects an event; carries the event Id to navigate to EventDetail.</summary>
    public event Action<long>? EventSelected;

    public ResultsViewModel(RunService runSvc) => _runSvc = runSvc;

    public async Task LoadAsync()
    {
        // No-op if no run is pinned; caller typically follows up with LoadForRunAsync.
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
}
