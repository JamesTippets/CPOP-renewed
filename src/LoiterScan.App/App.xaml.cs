using System.IO;
using System.Net.Http;
using System.Windows;
using LoiterScan.Acquisition.CelesTrak;
using LoiterScan.Acquisition.SpaceTrack;
using LoiterScan.Analytics;
using LoiterScan.App.Services;
using LoiterScan.App.ViewModels;
using LoiterScan.Core.Abstractions;
using LoiterScan.Data;
using LoiterScan.Engine;
using LoiterScan.Propagation.Sgp4;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoiterScan.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();

        // ── Infrastructure ─────────────────────────────────────────────────
        var dbDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoiterScan");
        var dbPath = Path.Combine(dbDir, "loiterscan.db");
        Directory.CreateDirectory(dbDir);

        sc.AddDbContextFactory<LoiterScanDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        // Live sources — created once; credentials are passed per-call for SpaceTrack
        sc.AddSingleton(_ => new CelesTrakCatalogSource(new HttpClient { Timeout = TimeSpan.FromMinutes(2) }));
        sc.AddSingleton<SpaceTrackCatalogSource>();

        // The pipeline reads from the local DB cache (populated by CatalogCacheService)
        sc.AddSingleton<DbCatalogSource>();
        sc.AddSingleton<ICatalogSource>(sp => sp.GetRequiredService<DbCatalogSource>());

        sc.AddSingleton<CatalogCacheService>();

        // ── Core services ──────────────────────────────────────────────────
        sc.AddSingleton<IPropagator, Sgp4Propagator>();
        sc.AddSingleton<DetectionPipeline>();
        sc.AddSingleton<ConfigService>();
        sc.AddSingleton<RunService>();
        sc.AddSingleton<CatalogStatusService>();
        sc.AddSingleton<RecurringPairsAnalyzer>();
        sc.AddSingleton<TrendAnalyzer>();

        // ── ViewModels ─────────────────────────────────────────────────────
        sc.AddSingleton<DashboardViewModel>();
        sc.AddSingleton<ResultsViewModel>();
        sc.AddSingleton<EventDetailViewModel>();
        sc.AddSingleton<CatalogViewModel>();
        sc.AddSingleton<ConfigurationViewModel>();
        sc.AddSingleton<AnalyticsViewModel>();
        sc.AddSingleton<ShellViewModel>();

        // ── Shell ──────────────────────────────────────────────────────────
        sc.AddSingleton<MainWindow>();

        _services = sc.BuildServiceProvider();

        // Apply migrations and seed default config on first launch
        var configPath = Path.Combine(AppContext.BaseDirectory, "config", "default-config.json");
        try
        {
            var factory = _services.GetRequiredService<IDbContextFactory<LoiterScanDbContext>>();
            await using var db = factory.CreateDbContext();
            await DbInitializer.ApplyAndSeedAsync(db, configPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Database initialisation failed:\n{ex.Message}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _services.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
