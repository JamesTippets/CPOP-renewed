using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Root view-model for the main window. Owns the nav rail and swaps ActiveViewModel to
/// drive the ContentControl in the shell (view-model-first navigation, spec §7).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty] private object? _activeViewModel;

    public DashboardViewModel     Dashboard     { get; }
    public ResultsViewModel       Results       { get; }
    public EventDetailViewModel   EventDetail   { get; }
    public CatalogViewModel       Catalog       { get; }
    public ConfigurationViewModel Configuration { get; }
    public AnalyticsViewModel     Analytics     { get; }

    public ShellViewModel(IServiceProvider sp)
    {
        _sp           = sp;
        Dashboard     = sp.GetRequiredService<DashboardViewModel>();
        Results       = sp.GetRequiredService<ResultsViewModel>();
        EventDetail   = sp.GetRequiredService<EventDetailViewModel>();
        Catalog       = sp.GetRequiredService<CatalogViewModel>();
        Configuration = sp.GetRequiredService<ConfigurationViewModel>();
        Analytics     = sp.GetRequiredService<AnalyticsViewModel>();

        // Wire cross-VM navigation events
        Dashboard.RunSelected    += OnRunSelected;
        Results.EventSelected    += OnEventSelected;
        EventDetail.NavigateBack += OnEventDetailBack;

        // Start on Dashboard
        ActiveViewModel = Dashboard;
        _ = Dashboard.LoadAsync();
    }

    [RelayCommand] private void NavigateDashboard()     { ActiveViewModel = Dashboard; }
    [RelayCommand] private async Task NavigateResults() { ActiveViewModel = Results; await Results.LoadAsync(); }
    [RelayCommand] private async Task NavigateCatalog() { ActiveViewModel = Catalog; await Catalog.LoadAsync(); }
    [RelayCommand] private async Task NavigateConfig()  { ActiveViewModel = Configuration; await Configuration.LoadAsync(); }
    [RelayCommand] private async Task NavigateAnalytics(){ ActiveViewModel = Analytics; await Analytics.LoadAsync(); }

    private async void OnRunSelected(long runId)
    {
        ActiveViewModel = Results;
        await Results.LoadForRunAsync(runId);
    }

    private async void OnEventSelected(long eventId)
    {
        ActiveViewModel = EventDetail;
        await EventDetail.LoadAsync(eventId);
    }

    private void OnEventDetailBack() => ActiveViewModel = Results;
}
