using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoiterScan.App.Services;
using LoiterScan.Data.Entities;

namespace LoiterScan.App.ViewModels;

/// <summary>
/// Configuration view: edit cascade thresholds, exclusion lists, and acquisition settings.
/// Validates monotonic thresholds (coarse > fine > detection) before saving (spec §6.2).
/// </summary>
public sealed partial class ConfigurationViewModel : ObservableObject
{
    private readonly ConfigService _configSvc;

    public static IReadOnlyList<string> DataSources { get; } = ["CelesTrak", "Space-Track"];

    // ── Cascade ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int    _horizonDays;
    [ObservableProperty] private int    _coarseStep;
    [ObservableProperty] private double _coarseThreshold;
    [ObservableProperty] private int    _fineStep;
    [ObservableProperty] private double _fineThreshold;
    [ObservableProperty] private int    _detectionStep;
    [ObservableProperty] private double _detectionThreshold;
    [ObservableProperty] private int    _coarseToFine;
    [ObservableProperty] private int    _fineToDetection;
    [ObservableProperty] private int    _loiterMinDuration;
    [ObservableProperty] private int    _loiterExcursion;

    // ── Pre-filter: regime multi-select ───────────────────────────────────────
    [ObservableProperty] private bool _regimeAll = true;
    [ObservableProperty] private bool _regimeLeo;
    [ObservableProperty] private bool _regimeMeo;
    [ObservableProperty] private bool _regimeHeo;
    [ObservableProperty] private bool _regimeGeo;

    // Prevents re-entrance when updating several flags at once (e.g. ALL clears others).
    private bool _updatingRegime;

    // When ALL is toggled on, clear all specific regimes.
    partial void OnRegimeAllChanged(bool value)
    {
        if (!value || _updatingRegime) return;
        _updatingRegime = true;
        try { RegimeLeo = RegimeMeo = RegimeHeo = RegimeGeo = false; }
        finally { _updatingRegime = false; }
        OnPropertyChanged(nameof(RegimeScopeDisplay));
    }

    // When any specific regime is toggled on, deselect ALL.
    partial void OnRegimeLeoChanged(bool value) { if (!_updatingRegime && value) ClearAll(); OnPropertyChanged(nameof(RegimeScopeDisplay)); }
    partial void OnRegimeMeoChanged(bool value) { if (!_updatingRegime && value) ClearAll(); OnPropertyChanged(nameof(RegimeScopeDisplay)); }
    partial void OnRegimeHeoChanged(bool value) { if (!_updatingRegime && value) ClearAll(); OnPropertyChanged(nameof(RegimeScopeDisplay)); }
    partial void OnRegimeGeoChanged(bool value) { if (!_updatingRegime && value) ClearAll(); OnPropertyChanged(nameof(RegimeScopeDisplay)); }

    private void ClearAll()
    {
        if (!RegimeAll) return;
        _updatingRegime = true;
        try { RegimeAll = false; }
        finally { _updatingRegime = false; }
        OnPropertyChanged(nameof(RegimeScopeDisplay));
    }

    /// <summary>Summary label shown on the dropdown toggle button.</summary>
    public string RegimeScopeDisplay
    {
        get
        {
            if (RegimeAll) return "ALL";
            var parts = new List<string>(4);
            if (RegimeLeo) parts.Add("LEO");
            if (RegimeMeo) parts.Add("MEO");
            if (RegimeHeo) parts.Add("HEO");
            if (RegimeGeo) parts.Add("GEO");
            return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
        }
    }

    private string ComputeRegimeScopeString()
    {
        if (RegimeAll) return "ALL";
        var parts = new List<string>(4);
        if (RegimeLeo) parts.Add("LEO");
        if (RegimeMeo) parts.Add("MEO");
        if (RegimeHeo) parts.Add("HEO");
        if (RegimeGeo) parts.Add("GEO");
        return parts.Count == 0 ? "ALL" : string.Join(",", parts);
    }

    private void SetRegimeScopeFromString(string value)
    {
        var scopes = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .Select(s => s.ToUpperInvariant())
                          .ToHashSet();
        bool all = scopes.Contains("ALL") || scopes.Count == 0;
        _updatingRegime = true;
        try
        {
            RegimeAll = all;
            RegimeLeo = !all && scopes.Contains("LEO");
            RegimeMeo = !all && scopes.Contains("MEO");
            RegimeHeo = !all && scopes.Contains("HEO");
            RegimeGeo = !all && scopes.Contains("GEO");
        }
        finally { _updatingRegime = false; }
        OnPropertyChanged(nameof(RegimeScopeDisplay));
    }

    // ── Pre-filter: other ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _excludeDebris;
    [ObservableProperty] private bool _excludeGroupPairsOnly;
    [ObservableProperty] private int  _maxEpochAgeDays = 3;

    // ── Acquisition ───────────────────────────────────────────────────────────
    [ObservableProperty] private string _acquisitionSource = "CelesTrak";
    [ObservableProperty] private bool   _refreshBeforeRun;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool   _credentialsVisible;

    /// <summary>Raised when the password changes programmatically (e.g. on source switch) so the view can sync the PasswordBox.</summary>
    public event Action<string>? PasswordSync;

    partial void OnAcquisitionSourceChanged(string value)
    {
        if (value.Equals("CelesTrak", StringComparison.OrdinalIgnoreCase))
        {
            Username           = string.Empty;
            Password           = string.Empty;
            CredentialsVisible = false;
            PasswordSync?.Invoke(string.Empty);
        }
        else
        {
            CredentialsVisible = true;
        }
    }

    // ── Exclusion lists (newline-delimited in the text boxes) ─────────────────
    [ObservableProperty] private string _excludedCountriesText = string.Empty;
    [ObservableProperty] private string _excludedGroupsText    = string.Empty;
    [ObservableProperty] private string _excludedIdsText       = string.Empty;

    // ── Excluded pairs (co-orbital / docked — managed independently of Save) ──
    public ObservableCollection<ExclPairEntity> ExcludedPairs { get; } = [];

    // ── Status ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private string _saveStatus      = string.Empty;
    [ObservableProperty] private bool   _isDirty;

    public ConfigurationViewModel(ConfigService configSvc) => _configSvc = configSvc;

    public async Task LoadAsync()
    {
        var entity                   = await _configSvc.GetParamEntityAsync();
        var (countries, groups, ids) = await _configSvc.GetExclusionsAsync();

        HorizonDays        = entity.HorizonDays;
        CoarseStep         = entity.CoarseStepMinutes;
        CoarseThreshold    = entity.CoarseThresholdKm;
        FineStep           = entity.FineStepMinutes;
        FineThreshold      = entity.FineThresholdKm;
        DetectionStep      = entity.DetectionStepMinutes;
        DetectionThreshold = entity.DetectionThresholdKm;
        CoarseToFine       = entity.CoarseToFineMinutes;
        FineToDetection    = entity.FineToDetectionMinutes;
        LoiterMinDuration  = entity.LoiterMinDurationMinutes;
        LoiterExcursion    = entity.LoiterExcursionAllowanceMinutes;

        ExcludeDebris          = entity.ExcludeDebris;
        ExcludeGroupPairsOnly  = entity.ExcludeGroupPairsOnly;
        SetRegimeScopeFromString(entity.RegimeScope);
        MaxEpochAgeDays = entity.MaxEpochAgeDays;

        // Map stored lower-case source id to display name
        AcquisitionSource = entity.AcquisitionSource.Equals("space-track", StringComparison.OrdinalIgnoreCase)
            ? "Space-Track" : "CelesTrak";
        RefreshBeforeRun   = entity.RefreshBeforeRun;
        Username           = entity.CredentialUsername ?? string.Empty;
        Password           = entity.CredentialPassword ?? string.Empty;
        CredentialsVisible = !AcquisitionSource.Equals("CelesTrak", StringComparison.OrdinalIgnoreCase);
        PasswordSync?.Invoke(Password);

        ExcludedCountriesText = string.Join("\n", countries);
        ExcludedGroupsText    = string.Join("\n", groups);
        ExcludedIdsText       = string.Join("\n", ids.Select(i => i.ToString()));

        var pairs = await _configSvc.GetExclPairsAsync();
        ExcludedPairs.Clear();
        foreach (var p in pairs) ExcludedPairs.Add(p);

        ValidationError = string.Empty;
        SaveStatus      = string.Empty;
        IsDirty         = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Validate()) return;

        var entity = new ConfigParamEntity
        {
            Id = 1,
            HorizonDays        = HorizonDays,
            CoarseStepMinutes  = CoarseStep,
            CoarseThresholdKm  = CoarseThreshold,
            FineStepMinutes    = FineStep,
            FineThresholdKm    = FineThreshold,
            DetectionStepMinutes = DetectionStep,
            DetectionThresholdKm = DetectionThreshold,
            CoarseToFineMinutes     = CoarseToFine,
            FineToDetectionMinutes  = FineToDetection,
            LoiterMinDurationMinutes        = LoiterMinDuration,
            LoiterExcursionAllowanceMinutes = LoiterExcursion,
            ExcludeDebris         = ExcludeDebris,
            ExcludeGroupPairsOnly = ExcludeGroupPairsOnly,
            RegimeScope           = ComputeRegimeScopeString(),
            MaxEpochAgeDays    = MaxEpochAgeDays,
            AcquisitionSource  = AcquisitionSource.Equals("Space-Track", StringComparison.OrdinalIgnoreCase)
                                     ? "space-track" : "celestrak",
            RefreshBeforeRun   = RefreshBeforeRun,
            CredentialUsername = string.IsNullOrEmpty(Username) ? null : Username,
            CredentialPassword = string.IsNullOrEmpty(Password) ? null : Password,
        };

        var countries = SplitLines(ExcludedCountriesText);
        var groups    = SplitLines(ExcludedGroupsText);
        var ids       = SplitLines(ExcludedIdsText)
                            .Where(s => long.TryParse(s, out _))
                            .Select(long.Parse)
                            .ToList();

        await _configSvc.SaveParamsAsync(entity);
        await _configSvc.SaveExclusionsAsync(countries, groups, ids);

        SaveStatus = "Saved.";
        IsDirty    = false;
    }

    [RelayCommand] private async Task ReloadAsync() => await LoadAsync();

    [RelayCommand]
    private async Task RemovePairAsync(ExclPairEntity pair)
    {
        await _configSvc.RemoveExclPairAsync(pair.Id);
        ExcludedPairs.Remove(pair);
    }

    private bool Validate()
    {
        ValidationError = string.Empty;

        if (CoarseThreshold <= FineThreshold)
        {
            ValidationError = $"Coarse threshold ({CoarseThreshold} km) must exceed fine threshold ({FineThreshold} km).";
            return false;
        }
        if (FineThreshold <= DetectionThreshold)
        {
            ValidationError = $"Fine threshold ({FineThreshold} km) must exceed detection threshold ({DetectionThreshold} km).";
            return false;
        }
        if (CoarseStep <= FineStep)
        {
            ValidationError = $"Coarse step ({CoarseStep} min) must exceed fine step ({FineStep} min).";
            return false;
        }
        if (FineStep <= DetectionStep)
        {
            ValidationError = $"Fine step ({FineStep} min) must exceed detection step ({DetectionStep} min).";
            return false;
        }
        if (HorizonDays < 1)
        {
            ValidationError = "Horizon must be at least 1 day.";
            return false;
        }
        if (AcquisitionSource.Equals("Space-Track", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(Username))
        {
            ValidationError = "Space-Track requires a username.";
            return false;
        }
        return true;
    }

    private static List<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
}
