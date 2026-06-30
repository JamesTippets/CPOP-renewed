using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoiterScan.Data;
using LoiterScan.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Catalog view: browse the local catalog with derived fields and freshness flags.
/// Loads all objects into an ObservableCollection; WPF DataGrid virtualization handles rendering.
/// </summary>
public sealed partial class CatalogViewModel : ObservableObject
{
    private readonly IDbContextFactory<LoiterScanDbContext> _factory;
    private List<CatalogObjectEntity> _allObjects = [];

    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private int    _ownerCount;
    [ObservableProperty] private int    _typeCount;
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private bool   _isLoading;
    [ObservableProperty] private string _lastIngested = string.Empty;

    public ObservableCollection<CatalogObjectEntity> Objects { get; } = [];

    public CatalogViewModel(IDbContextFactory<LoiterScanDbContext> factory) => _factory = factory;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await using var db = _factory.CreateDbContext();
            _allObjects = await db.CatalogObjects
                .Include(x => x.Groups)
                .OrderBy(x => x.NoradId)
                .ToListAsync();

            TotalCount   = _allObjects.Count;
            OwnerCount   = _allObjects.Count(o => !string.IsNullOrEmpty(o.Owner));
            TypeCount    = _allObjects.Count(o => !string.IsNullOrEmpty(o.ObjectType));
            LastIngested = _allObjects.Count > 0
                ? _allObjects.Max(o => o.IngestedAt).ToString("yyyy-MM-dd HH:mm UTC")
                : string.Empty;

            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
    }

    private void ApplyFilter()
    {
        Objects.Clear();
        var filter = FilterText.Trim();
        var source = string.IsNullOrEmpty(filter)
            ? _allObjects
            : _allObjects.Where(o =>
                (o.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                o.NoradId.ToString().Contains(filter) ||
                (o.Owner?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                o.Regime.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var obj in source) Objects.Add(obj);
    }
}
