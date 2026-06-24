# Loiter Scan

Stand-alone Windows (WPF / .NET 8) application that screens the satellite catalog for
**serendipitous loitering** of satellite pairs — pairs within 5 km for ≥ 1 hour that are not
intentional formations, constellations, or co-locations.

See **[docs/loiter-detection-spec.md](docs/loiter-detection-spec.md)** for the full specification.

## Solution layout

| Project | Role |
|---|---|
| `src/LoiterScan.Core` | Abstractions + models (no dependencies) |
| `src/LoiterScan.Propagation` | `IPropagator` implementations (Vallado SGP4 v1; AstroStds XP later) |
| `src/LoiterScan.Acquisition` | `ICatalogSource` implementations (CelesTrak v1; Space-Track later) |
| `src/LoiterScan.Data` | EF Core / SQLite (DbContext, migrations, seeding) |
| `src/LoiterScan.Engine` | The detection cascade |
| `src/LoiterScan.Analytics` | Cross-run aggregation (recurring pairs, trends) |
| `src/LoiterScan.App` | WPF GUI + composition root |
| `tests/` | Unit tests |
| `config/default-config.json` | Versioned default-config seed |

## Build (Windows only)

```
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/LoiterScan.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

> The GUI uses WPF, which builds only on Windows.

## Notes

- Package versions in the `.csproj` files float (e.g. `8.*`); run `dotnet restore` and pin them as you prefer.
- The local SQLite catalog (`*.db`) is regenerated and is git-ignored.
- The engine depends only on the `Core` interfaces; concrete propagator/source are wired in `LoiterScan.App` (the composition root), which is what keeps the SGP4→XP and CelesTrak→Space-Track swaps localized.
