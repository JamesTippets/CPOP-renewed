# Build Order

A roadmap for implementing the application phase by phase. Read this alongside
[`loiter-detection-spec.md`](loiter-detection-spec.md) — each phase points to the spec section
that defines the behavior.

The order is **bottom-up**, following the project dependency graph (spec §9.2): each phase depends
only on phases above it, so the solution stays buildable and testable throughout.

## Working agreement

- Before starting a phase, read the referenced spec section.
- Each phase has an **acceptance** target. Several map to the skipped placeholder tests in
  `tests/` — implement the behavior, then remove the `Skip` and make the test pass.
- Keep the dependency direction intact: `Engine`, `Analytics`, and `Data` depend only on
  `LoiterScan.Core` interfaces. Concrete propagator/source types are wired only in
  `LoiterScan.App` (the composition root).
- Run `dotnet build` and `dotnet test` before committing. Work on a feature branch and open a
  merge request per phase; `main` is protected.
- The GUI (`LoiterScan.App`) requires Windows; the libraries target `net8.0` and build anywhere.

---

## Phase 0 — Baseline

**Goal:** confirm the skeleton restores, builds, and tests green before any feature work.

- `dotnet restore`, then pin the floating package versions (`8.*`, `5.*`, etc.) to the latest
  stable versions the restore selected.
- `dotnet build -c Release` and `dotnet test -c Release` (tests are skipped placeholders and
  should report as skipped, not failed).
- Confirm the GitLab pipeline runs on the Windows runner.

**Acceptance:** clean build; CI green; versions pinned and committed.

---

## Phase 1 — Core models (mostly complete)

**Spec:** §5.1, §6.1 · **Depends on:** —

`LoiterScan.Core` already defines `MeanElements`, `OrbitState`, `PairKey`, `CatalogObject`,
`LoiteringEvent`, `IPropagator`, and `ICatalogSource`. Extend only as later phases require (e.g.
a `RunConfig` record mirroring `config/default-config.json`).

**Acceptance:** Core compiles; any added types are covered by the consuming phase's tests.

---

## Phase 2 — Propagation (Vallado SGP4)

**Spec:** §5.1 · **Depends on:** Core

- Implement `Sgp4Propagator : IPropagator` using the Vallado public-domain reference SGP4.
- It must accept `MeanElements` **directly** — no TLE-string round-trip (that would reintroduce
  the 5-digit catalog-number limit OMM removes).

**Acceptance:** un-skip and pass `Sgp4PropagatorTests`, verified against the published SGP4
verification cases (Spacetrack Report #3 / Vallado verification TLEs) within their documented
tolerances.

**Gotcha:** validate against the standard reference set, not hand-rolled expectations.

---

## Phase 3 — Data (EF Core / SQLite)

**Spec:** §6 · **Depends on:** Core

- Define entities for `config_params`, the `excl_*` lists, `runs` (with the config snapshot),
  `loitering_events` (with indexed `pair_key`), and the local catalog.
- Implement `LoiterScanDbContext`; create the first migration; seed config on first launch from
  `config/default-config.json`.
- Store NORAD ids as a wide integer (9-digit ready).

**Acceptance:** a migration creates the schema; seeding loads the defaults; a round-trip test
writes and reads a `runs` row with its config snapshot.

---

## Phase 4 — Acquisition (CelesTrak)

**Spec:** §4 · **Depends on:** Core, Data

- Implement `CelesTrakCatalogSource : ICatalogSource`: fetch GP data as **OMM (JSON)**, join
  SATCAT metadata and GROUP membership on NORAD id, and pull the full catalog including debris.
- Compute derived fields on ingest: semi-major axis, apogee/perigee radius, regime, epoch age.
- Dedupe to newest-per-object; flag decayed objects; handle objects present in GP but missing
  from SATCAT without failing the join.

**Acceptance:** un-skip and pass `CelesTrakCatalogSourceTests` — parse a saved sample OMM payload,
join a sample SATCAT record, and populate `CatalogObject`s with correct derived fields.

**Gotcha:** ingest OMM/GP, never 2-line TLE text.

---

## Phase 5 — Engine (the cascade)

**Spec:** §5 · **Depends on:** Core (uses `IPropagator`, `ICatalogSource` via DI)

Implement the four tiers and the windowed handoff:

1. **Pre-filter** — exclusions (countries, ids, groups, debris toggle), apogee/perigee gating,
   regime scope toggle.
2. **Coarse** — 15 min / 50 km, per-timestep spatial index (k-d tree or grid) + radius query;
   emit candidate pairs **with time windows**.
3. **Fine** — 5 min / 25 km, windowed to coarse windows ± 30 min.
4. **Detection** — 1 min / 5 km, windowed ± 10 min; contiguous ≥ 1 h with ≤ 5 min bridged
   excursions (bridged minutes count; reset only past 5 min).

Run off the UI thread with `IProgress<PipelineProgress>` and `CancellationToken`.

**Acceptance:** un-skip and pass `DetectionPipelineTests` — construct a synthetic pair engineered
to sit ≤ 5 km for > 1 h and assert it is detected; assert a single slow flyby is **not**. Add a
coarse-pass performance sanity check on a representative object count.

---

## Phase 6 — Analytics

**Spec:** §8 · **Depends on:** Core, Data

- `RecurringPairsAnalyzer`: group events by `pair_key` across runs; compute recurrence count,
  first/last seen, min range, and min-range trend. Key on close-approach time to separate
  reconfirmation of one event from genuinely repeated episodes.
- `TrendAnalyzer`: events per run, new vs recurring, breakdowns by regime/owner — **config-scoped**
  so threshold changes don't confound the trend.

**Acceptance:** un-skip and pass `RecurringPairsAnalyzerTests` — seed several runs and verify
recurrence counting and the closing/opening trend; verify trends exclude cross-config comparison.

---

## Phase 7 — App / GUI (WPF)

**Spec:** §7 · **Depends on:** everything (composition root)

- MVVM (CommunityToolkit.Mvvm) shell with the five views: Dashboard/Run, Results, Event detail,
  Catalog, Configuration (see Figures 2–4 in the spec).
- Wire DI in `App.xaml.cs`: `IPropagator → Sgp4Propagator`, `ICatalogSource → CelesTrakCatalogSource`,
  `LoiterScanDbContext`, `DetectionPipeline`, analytics, and the view-models.
- Run trigger with the refresh-first override; live per-phase progress with cancellation.
- Event detail: RIC relative-motion plot + range-vs-time plot (ScottPlot). Catalog/Results grids
  use UI virtualization. Config editor validates monotonic thresholds (50 > 25 > 5) and steps.

**Acceptance:** an end-to-end run launched from the UI ingests, runs the cascade, persists a run
with its config snapshot, and displays events and the event-detail plots.

---

## After Phase 7

Then the future-work items in spec §11: the Space-Track source, the SGP4-XP propagator via
AstroStds interop, the optional 3D inertial view, and covariance-aware confidence — each an
additive implementation behind an existing interface.
