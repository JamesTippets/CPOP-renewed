using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LoiterScan.App.ViewModels;
using Microsoft.Win32;

namespace LoiterScan.App.Views;

public partial class EventDetailView : UserControl
{
    private EventDetailViewModel? _vm;
    private bool _cesiumPageReady;

    private ScottPlot.Plottables.Marker? _rangePlayhead;
    private ScottPlot.Plottables.Marker? _ricPlayhead;

    public EventDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;

        // After ScottPlot's own MouseWheel handler zooms the chart, mark the
        // event handled so it stops bubbling to the outer ScrollViewer.
        // PreviewMouseWheel would fire before ScottPlot and kill zoom; MouseWheel
        // fires after, so ScottPlot zooms first and the page never scrolls.
        RicPlot.MouseWheel   += (_, e) => e.Handled = true;
        RangePlot.MouseWheel += (_, e) => e.Handled = true;

        RicPlot.MouseLeftButtonDown   += OnChartLeftClick;
        RangePlot.MouseLeftButtonDown += OnChartLeftClick;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await CesiumView.EnsureCoreWebView2Async();
            CesiumView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
            CesiumView.CoreWebView2.NavigationCompleted  += OnCesiumNavigationCompleted;
            CesiumView.CoreWebView2.WebMessageReceived   += OnCesiumTick;
            CesiumView.NavigateToString(CesiumHtml());
        }
        catch (Exception ex)
        {
            // Show the error inside the panel instead of silently leaving it white.
            try
            {
                CesiumView.NavigateToString(
                    "<body style=\"background:#1a1a2e;color:#f88;font-family:monospace;padding:16px\">" +
                    "WebView2 init failed: " +
                    System.Net.WebUtility.HtmlEncode(ex.GetType().Name + ": " + ex.Message) +
                    "</body>");
            }
            catch { }
        }
    }

    private async void OnCesiumNavigationCompleted(
        object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
    {
        _cesiumPageReady = true;
        await InjectOrbitPathsAsync();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PlotsReady -= OnPlotsReady;
        _vm = e.NewValue as EventDetailViewModel;
        if (_vm is not null) _vm.PlotsReady += OnPlotsReady;
    }

    private void OnPlotsReady(object? sender, EventArgs e) => Dispatcher.Invoke(BuildPlots);

    private void BuildPlots()
    {
        if (_vm is null) return;

        // --- RIC plot ---
        RicPlot.Plot.Clear();
        RicPlot.Plot.Axes.Rules.Clear();
        if (_vm.HasData && _vm.RicR is not null)
        {
            RicPlot.Plot.DataBackground.Color = new ScottPlot.Color(45, 45, 45, 255);
            var ricStyle = RicPlot.Plot.GetStyle();
            ricStyle.AxisColor          = new ScottPlot.Color(200, 200, 200, 255);
            ricStyle.GridMajorLineColor = new ScottPlot.Color(75, 75, 75, 255);
            RicPlot.Plot.SetStyle(ricStyle);

            var cmap = new ScottPlot.Colormaps.Turbo();
            int n = _vm.RicR.Length;
            for (int i = 0; i < n - 1; i++)
            {
                double t = (double)i / Math.Max(n - 2, 1);
                var seg = RicPlot.Plot.Add.ScatterLine(
                    new[] { _vm.RicR[i], _vm.RicR[i + 1] },
                    new[] { _vm.RicI![i], _vm.RicI[i + 1] },
                    cmap.GetColor(t));
                seg.LineWidth = 2f;
            }

            bool firstAboveLast = _vm.RicLabelPoints.Length >= 3 &&
                                  _vm.RicLabelPoints[0].I > _vm.RicLabelPoints[2].I;
            for (int i = 0; i < _vm.RicLabelPoints.Length; i++)
            {
                var (r, ic, timeUtc) = _vm.RicLabelPoints[i];
                var txt = RicPlot.Plot.Add.Text(timeUtc.ToString("yyyy-MM-dd\nHH:mm UTC"), r, ic);
                txt.LabelFontSize        = 8.5f;
                txt.LabelBackgroundColor = ScottPlot.Colors.White;
                txt.LabelBorderColor     = ScottPlot.Colors.Gray;
                txt.LabelBorderWidth     = 1f;
                txt.LabelBorderRadius    = 4f;
                txt.LabelPadding         = 4f;
                (txt.LabelAlignment, txt.LabelOffsetX, txt.LabelOffsetY) = i switch {
                    0 when firstAboveLast  => (ScottPlot.Alignment.UpperLeft,   8f,   8f),
                    0                      => (ScottPlot.Alignment.LowerLeft,   8f,  -8f),
                    1                      => (ScottPlot.Alignment.LowerCenter, 0f, -12f),
                    _ when !firstAboveLast => (ScottPlot.Alignment.UpperRight, -8f,   8f),
                    _                      => (ScottPlot.Alignment.LowerRight, -8f,  -8f),
                };
            }

            double maxAbsR = 0;
            foreach (var v in _vm.RicR) if (Math.Abs(v) > maxAbsR) maxAbsR = Math.Abs(v);
            double firstI = _vm.RicI![0];
            double lastI  = _vm.RicI[n - 1];
            var ricLimits = new ScottPlot.AxisLimits(
                -(maxAbsR + 1), maxAbsR + 1,
                Math.Min(firstI, lastI) - 1,
                Math.Max(firstI, lastI) + 1);
            RicPlot.Plot.Axes.SetLimits(ricLimits);
            RicPlot.Plot.Axes.Rules.Add(
                new ScottPlot.AxisRules.MaximumBoundary(
                    RicPlot.Plot.Axes.Bottom, RicPlot.Plot.Axes.Left, ricLimits));

            RicPlot.Plot.XLabel("Radial (km)");
            RicPlot.Plot.YLabel("In-track (km)");

            _ricPlayhead = RicPlot.Plot.Add.Marker(_vm.RicR![0], _vm.RicI![0]);
            _ricPlayhead.Color = ScottPlot.Colors.White;
            _ricPlayhead.Size  = 11;
            _ricPlayhead.Shape = ScottPlot.MarkerShape.OpenCircle;
        }
        else { _ricPlayhead = null; }
        RicPlot.Refresh();

        // --- Range vs Time plot ---
        RangePlot.Plot.Clear();
        RangePlot.Plot.Axes.Rules.Clear();
        if (_vm.HasData && _vm.RangeKm is not null)
        {
            RangePlot.Plot.Add.Scatter(_vm.RangeTimeOADate!, _vm.RangeKm);

            var vStart  = RangePlot.Plot.Add.VerticalLine(_vm.LoiterStartOA);
            vStart.Color = ScottPlot.Colors.Green;
            var vEnd    = RangePlot.Plot.Add.VerticalLine(_vm.LoiterEndOA);
            vEnd.Color   = ScottPlot.Colors.Red;
            var hThresh  = RangePlot.Plot.Add.HorizontalLine(_vm.ThresholdKm);
            hThresh.Color = ScottPlot.Colors.Orange;

            var tickGen = new ScottPlot.TickGenerators.DateTimeFixedInterval(
                new ScottPlot.TickGenerators.TimeUnits.Minute(), 30,
                new ScottPlot.TickGenerators.TimeUnits.Minute(), 30,
                dt => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute / 30 * 30, 0, DateTimeKind.Utc));
            tickGen.LabelFormatter = dt => dt.ToString("yyyy-MM-dd\nHH:mm (UTC)");
            RangePlot.Plot.Axes.Bottom.TickGenerator = tickGen;
            RangePlot.Plot.Axes.Bottom.TickLabelStyle.FontSize  = 8f;
            RangePlot.Plot.Axes.Bottom.TickLabelStyle.Rotation  = 90;
            RangePlot.Plot.Axes.Bottom.TickLabelStyle.Alignment = ScottPlot.Alignment.MiddleLeft;

            double[] oad = _vm.RangeTimeOADate!;
            var rangeLimits = new ScottPlot.AxisLimits(oad[0], oad[^1], 0, 10);
            RangePlot.Plot.Axes.SetLimits(rangeLimits);
            RangePlot.Plot.Axes.Rules.Add(
                new ScottPlot.AxisRules.MaximumBoundary(
                    RangePlot.Plot.Axes.Bottom, RangePlot.Plot.Axes.Left, rangeLimits));

            RangePlot.Plot.XLabel("Time (UTC)");
            RangePlot.Plot.YLabel("Range (km)");

            int phIdx = (int)Math.Round((_vm.LoiterStartOA - oad[0]) * 1440.0);
            phIdx = Math.Clamp(phIdx, 0, _vm.RangeKm.Length - 1);
            _rangePlayhead = RangePlot.Plot.Add.Marker(
                oad[phIdx], _vm.RangeKm[phIdx]);
            _rangePlayhead.Color = ScottPlot.Colors.Black;
            _rangePlayhead.Size  = 11;
            _rangePlayhead.Shape = ScottPlot.MarkerShape.FilledCircle;
        }
        else { _rangePlayhead = null; }
        RangePlot.Refresh();

        _ = InjectOrbitPathsAsync();
    }

    private void OnCesiumTick(
        object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (msg == "export-czml")
        {
            Dispatcher.Invoke(ExportCzml);
            return;
        }
        if (!double.TryParse(
                msg,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double oaDate)) return;
        Dispatcher.Invoke(() => UpdatePlayhead(oaDate));
    }

    private void UpdatePlayhead(double oaDate)
    {
        if (_vm?.RangeTimeOADate is not { Length: > 0 } times) return;

        oaDate = Math.Clamp(oaDate, times[0], times[^1]);

        // Shared index: data is at 1-min steps from times[0].
        int idx = (int)Math.Round((oaDate - times[0]) * 1440.0);
        idx = Math.Clamp(idx, 0, times.Length - 1);

        if (_rangePlayhead is not null && _vm.RangeKm is not null)
        {
            _rangePlayhead.Location = new ScottPlot.Coordinates(oaDate, _vm.RangeKm[idx]);
            RangePlot.Refresh();
        }

        if (_ricPlayhead is not null && _vm.RicR is not null && _vm.RicI is not null)
        {
            _ricPlayhead.Location = new ScottPlot.Coordinates(_vm.RicR[idx], _vm.RicI[idx]);
            RicPlot.Refresh();
        }
    }


    private void OnChartLeftClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.RangeTimeOADate is not { Length: > 0 } times ||
            _vm.RicR is null || _vm.RangeKm is null) return;

        int idx;
        if (ReferenceEquals(sender, RicPlot))
        {
            var pos    = e.GetPosition(RicPlot);
            var click  = RicPlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);
            var limits = RicPlot.Plot.Axes.GetLimits();
            double xs  = limits.HorizontalSpan, ys = limits.VerticalSpan;
            idx = FindNearest(i => {
                double dx = (_vm.RicR![i] - click.X) / xs;
                double dy = (_vm.RicI![i] - click.Y) / ys;
                return dx * dx + dy * dy;
            }, times.Length);
        }
        else
        {
            var pos    = e.GetPosition(RangePlot);
            var click  = RangePlot.Plot.GetCoordinates((float)pos.X, (float)pos.Y);
            var limits = RangePlot.Plot.Axes.GetLimits();
            double xs  = limits.HorizontalSpan, ys = limits.VerticalSpan;
            idx = FindNearest(i => {
                double dx = (times[i] - click.X) / xs;
                double dy = (_vm.RangeKm![i] - click.Y) / ys;
                return dx * dx + dy * dy;
            }, times.Length);
        }

        ApplySelection(idx);
    }

    private static int FindNearest(Func<int, double> distSq, int count)
    {
        int best = 0;
        double bestDist = double.MaxValue;
        for (int i = 0; i < count; i++)
        {
            double d = distSq(i);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private void ApplySelection(int idx)
    {
        if (_vm?.RangeTimeOADate is not { Length: > 0 } times) return;
        _ = JumpCesiumAsync(times[idx]);
    }

    private async Task JumpCesiumAsync(double oaDate)
    {
        if (!_cesiumPageReady) return;
        var iso = DateTime.SpecifyKind(DateTime.FromOADate(oaDate), DateTimeKind.Utc).ToString("O");
        await CesiumView.CoreWebView2.ExecuteScriptAsync(
            $"if(window.jumpToTime)window.jumpToTime('{iso}');");
    }

    private async Task InjectOrbitPathsAsync()
    {
        if (_vm?.OrbitEcefA is null || !_cesiumPageReady) return;

        var data = new
        {
            clockStart = _vm.ClockStartIso,
            clockStop  = _vm.ClockStopIso,
            caTimeIso  = _vm.CaTimeIso,
            caLabel    = _vm.CaLabel,
            a = new {
                name         = _vm.SatLabelA,
                startIso     = _vm.OrbitStartIsoA,
                positions    = _vm.OrbitEcefA,
                eciPositions = _vm.OrbitEciA,
                labelPos     = _vm.SatALabelEcef,
                labelPosEci  = _vm.SatALabelEci,
            },
            b = new {
                name         = _vm.SatLabelB,
                startIso     = _vm.OrbitStartIsoB,
                positions    = _vm.OrbitEcefB,
                eciPositions = _vm.OrbitEciB,
                labelPos     = _vm.SatBLabelEcef,
                labelPosEci  = _vm.SatBLabelEci,
            },
        };
        var json = JsonSerializer.Serialize(data);
        await CesiumView.CoreWebView2.ExecuteScriptAsync(
            $"if(window.addOrbitPaths)window.addOrbitPaths({json});");
    }

    // ── CZML export ─────────────────────────────────────────────────────────

    private void ExportCzml()
    {
        if (_vm?.OrbitEcefA is null || _vm.OrbitEcefB is null) return;

        var caUtc = string.IsNullOrEmpty(_vm.CaTimeIso)
            ? DateTime.UtcNow
            : DateTime.Parse(_vm.CaTimeIso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        var defaultName = $"{caUtc:yyyy-MM-dd HHmm} UTC Event {SanitizeFilename(_vm.SatLabelA)} vs {SanitizeFilename(_vm.SatLabelB)}";

        var dialog = new SaveFileDialog
        {
            Title      = "Export CZML",
            Filter     = "CZML files (*.czml)|*.czml|All files (*.*)|*.*",
            DefaultExt = ".czml",
            FileName   = defaultName,
        };

        if (dialog.ShowDialog() != true) return;

        File.WriteAllText(dialog.FileName, GenerateCzml(), new System.Text.UTF8Encoding(false));
    }

    private string GenerateCzml()
    {
        var vm = _vm!;
        var packets = new List<object>
        {
            new {
                id      = "document",
                name    = $"{vm.SatLabelA} vs {vm.SatLabelB}",
                version = "1.0",
                clock   = new {
                    interval    = $"{vm.ClockStartIso}/{vm.ClockStopIso}",
                    currentTime = vm.ClockStartIso,
                    multiplier  = 60,
                    range       = "LOOP_STOP",
                    step        = "SYSTEM_CLOCK_MULTIPLIER",
                },
            },
            BuildSatPacket("satA", vm.SatLabelA, vm.ClockStartIso, vm.ClockStopIso,
                vm.OrbitStartIsoA, vm.OrbitEcefA!, [0, 255, 255, 255]),
            BuildSatPacket("satB", vm.SatLabelB, vm.ClockStartIso, vm.ClockStopIso,
                vm.OrbitStartIsoB, vm.OrbitEcefB!, [255, 140, 0, 255]),
        };

        if (!string.IsNullOrEmpty(vm.CaTimeIso) && vm.SatALabelEcef.Length == 3)
            packets.Add(BuildCaPacket(vm.CaTimeIso, vm.SatALabelEcef, vm.CaLabel));

        return JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object BuildSatPacket(
        string id, string name,
        string availStart, string availStop,
        string epochIso, double[] ecef,
        int[] rgba)
    {
        int steps = ecef.Length / 3;
        var cartesian = new double[steps * 4];
        for (int i = 0; i < steps; i++)
        {
            cartesian[i * 4]     = i * 60.0;
            cartesian[i * 4 + 1] = ecef[i * 3];
            cartesian[i * 4 + 2] = ecef[i * 3 + 1];
            cartesian[i * 4 + 3] = ecef[i * 3 + 2];
        }

        return new {
            id,
            name,
            availability = $"{availStart}/{availStop}",
            label = new {
                text         = name,
                font         = "13px sans-serif",
                fillColor    = new { rgba },
                outlineColor = new { rgba = new[] {0, 0, 0, 255} },
                outlineWidth = 2,
                style        = "FILL_AND_OUTLINE",
                verticalOrigin = "BOTTOM",
                pixelOffset  = new { cartesian2 = new[] {0, -12} },
            },
            point = new {
                pixelSize    = 9,
                color        = new { rgba },
                outlineColor = new { rgba = new[] {0, 0, 0, 255} },
                outlineWidth = 1,
            },
            path = new {
                material = new {
                    solidColor = new {
                        color = new { rgba = new[] {rgba[0], rgba[1], rgba[2], 230} },
                    },
                },
                width      = 2,
                leadTime   = steps * 60,
                trailTime  = steps * 60,
                resolution = 30,
            },
            position = new {
                epoch          = epochIso,
                referenceFrame = "FIXED",
                cartesian,
            },
        };
    }

    private static object BuildCaPacket(string caIso, double[] ecef, string label)
    {
        return new {
            id   = "ca-marker",
            name = "Closest Approach",
            label = new {
                text            = label,
                font            = "11px sans-serif",
                fillColor       = new { rgba = new[] {255, 230, 0, 255} },
                outlineColor    = new { rgba = new[] {0, 0, 0, 255} },
                outlineWidth    = 2,
                style           = "FILL_AND_OUTLINE",
                verticalOrigin  = "BOTTOM",
                pixelOffset     = new { cartesian2 = new[] {0, -14} },
                showBackground  = true,
                backgroundColor = new { rgba = new[] {0, 0, 26, 140} },
            },
            point = new {
                pixelSize    = 11,
                color        = new { rgba = new[] {255, 230, 0, 255} },
                outlineColor = new { rgba = new[] {0, 0, 0, 255} },
                outlineWidth = 2,
            },
            position = new {
                cartesian = ecef,
            },
        };
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
    }

    private static string CesiumHtml() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <script src="https://cdn.jsdelivr.net/npm/cesium@1.95.0/Build/Cesium/Cesium.js"></script>
            <link href="https://cdn.jsdelivr.net/npm/cesium@1.95.0/Build/Cesium/Widgets/widgets.css" rel="stylesheet">
            <style>
                html, body, #cesiumContainer {
                    width: 100%; height: 100%;
                    margin: 0; padding: 0;
                    overflow: hidden;
                    background: #0d0d1a;
                }
            </style>
        </head>
        <body>
            <div id="cesiumContainer"></div>
            <script>
            (function () {
                try {
                    Cesium.Ion.defaultAccessToken = undefined;

                    var viewer = new Cesium.Viewer('cesiumContainer', {
                        imageryProvider: new Cesium.TileMapServiceImageryProvider({
                            url: Cesium.buildModuleUrl('Assets/Textures/NaturalEarthII'),
                        }),
                        terrainProvider:      new Cesium.EllipsoidTerrainProvider(),
                        baseLayerPicker:      false,
                        animation:            true,
                        timeline:             true,
                        geocoder:             false,
                        homeButton:           true,
                        sceneModePicker:      false,
                        navigationHelpButton: false,
                        infoBox:              false,
                        selectionIndicator:   false,
                        creditContainer:      document.createElement('div'),
                    });

                    viewer.scene.globe.enableLighting = true;
                    viewer.scene.backgroundColor = Cesium.Color.fromCssColorString('#0d0d1a');

                    window._viewer     = viewer;
                    window._tracking   = false;
                    window._frame      = 'ECEF';
                    window._satAEntity = null;
                    window._orbitData  = null;

                    // ── Camera-tracking toggle (left of home button) ──────────────────────
                    var trackBtn = document.createElement('button');
                    trackBtn.className = 'cesium-button cesium-toolbar-button';
                    trackBtn.style.cssText = 'min-width:110px;font-size:11px;padding:0 6px;';
                    trackBtn.textContent = 'Not following';
                    trackBtn.title = 'Toggle camera lock on Sat A';
                    trackBtn.onclick = function () {
                        window._tracking = !window._tracking;
                        if (window._tracking && window._satAEntity) {
                            viewer.trackedEntity = window._satAEntity;
                            trackBtn.textContent = 'Following...';
                        } else {
                            viewer.trackedEntity = undefined;
                            viewer.camera.flyHome();
                            trackBtn.textContent = 'Not following';
                        }
                    };
                    (function () {
                        var bar  = viewer.container.querySelector('.cesium-viewer-toolbar');
                        if (!bar) return;
                        var home = bar.querySelector('.cesium-home-button');
                        if (home) bar.insertBefore(trackBtn, home);
                        else      bar.appendChild(trackBtn);
                    }());

                    // ── Export CZML button ────────────────────────────────────────────────
                    var exportBtn = document.createElement('button');
                    exportBtn.className = 'cesium-button cesium-toolbar-button';
                    exportBtn.style.cssText = 'min-width:100px;font-size:11px;padding:0 6px;';
                    exportBtn.textContent = 'Export CZML';
                    exportBtn.title = 'Export current scene as a CZML file';
                    exportBtn.onclick = function () {
                        try { window.chrome.webview.postMessage('export-czml'); } catch (_) {}
                    };
                    (function () {
                        var bar  = viewer.container.querySelector('.cesium-viewer-toolbar');
                        if (!bar) return;
                        var home = bar.querySelector('.cesium-home-button');
                        if (home) bar.insertBefore(exportBtn, home);
                        else      bar.appendChild(exportBtn);
                    }());

                    // ── ECEF / ECI frame toggle (upper-left overlay) ─────────────────────
                    var frameDiv = document.createElement('div');
                    frameDiv.style.cssText =
                        'position:absolute;top:8px;left:8px;z-index:1000;' +
                        'background:rgba(30,30,48,0.88);border-radius:4px;' +
                        'padding:5px 10px;display:flex;gap:10px;align-items:center;' +
                        'font-family:Roboto,sans-serif;font-size:12px;color:#ccc;' +
                        'box-shadow:0 1px 4px rgba(0,0,0,0.6);user-select:none;';
                    frameDiv.innerHTML =
                        'Frame:&nbsp;' +
                        '<label style="cursor:pointer;color:#eee">' +
                            '<input type="radio" name="cesFrame" value="ECEF" checked>&nbsp;ECEF' +
                        '</label>' +
                        '<label style="cursor:pointer;color:#eee">' +
                            '<input type="radio" name="cesFrame" value="ECI">&nbsp;ECI' +
                        '</label>';
                    viewer.container.appendChild(frameDiv);
                    [].forEach.call(frameDiv.querySelectorAll('input[name=cesFrame]'), function (r) {
                        r.addEventListener('change', function () {
                            if (!r.checked) return;
                            window._frame = r.value;
                            if (window._orbitData) window._updateDisplay(window._orbitData);
                            if (window._tracking && window._satAEntity)
                                viewer.trackedEntity = window._satAEntity;
                        });
                    });

                    // ── Chart playhead — post current OADate to WPF on each clock tick ───
                    // Throttled to 200 ms wall time so the Dispatcher queue stays light.
                    // OADate = JD − 2415018.5  (days since 1899-12-30 00:00 UTC)
                    var _lastTickWall = 0;
                    viewer.clock.onTick.addEventListener(function (clock) {
                        var now = Date.now();
                        if (now - _lastTickWall < 200) return;
                        _lastTickWall = now;
                        var jd = clock.currentTime.dayNumber
                               + clock.currentTime.secondsOfDay / 86400.0;
                        var oa = jd - 2415018.5;
                        try { window.chrome.webview.postMessage(oa.toString()); } catch (_) {}
                    });

                    // ── Orbit injection (called from C# once data is ready) ──────────────
                    window.addOrbitPaths = function (data) {
                        window._orbitData = data;
                        window._updateDisplay(data);

                        var start = Cesium.JulianDate.fromIso8601(data.clockStart);
                        var stop  = Cesium.JulianDate.fromIso8601(data.clockStop);
                        viewer.clock.startTime   = start.clone();
                        viewer.clock.stopTime    = stop.clone();
                        viewer.clock.currentTime = start.clone();
                        viewer.clock.clockRange  = Cesium.ClockRange.LOOP_STOP;
                        viewer.clock.multiplier  = 60;
                        viewer.timeline.zoomTo(start, stop);

                        // Yellow CA tick overlaid on the timeline bar.
                        // Uses pixel positioning recomputed via ResizeObserver so the tick
                        // stays accurate when the viewer is resized (CSS % can drift if
                        // Cesium's internal timeline resize shifts the canvas bounds).
                        (function () {
                            var old = document.getElementById('ca-tl-marker');
                            if (old) {
                                if (window._caTickObs) {
                                    window._caTickObs.disconnect();
                                    window._caTickObs = null;
                                }
                                old.parentNode.removeChild(old);
                            }

                            if (!data.caTimeIso) return;
                            var caJd   = Cesium.JulianDate.fromIso8601(data.caTimeIso);
                            var total  = Cesium.JulianDate.secondsDifference(stop, start);
                            var offset = Cesium.JulianDate.secondsDifference(caJd, start);
                            var frac   = Math.max(0, Math.min(1, offset / total));

                            // Prefer the inner rendering div; fall back to the outer container.
                            var tlEl = viewer.container.querySelector('.cesium-timeline-main')
                                    || viewer.timeline.container;

                            var tick = document.createElement('div');
                            tick.id  = 'ca-tl-marker';
                            tick.style.cssText =
                                'position:absolute;top:0;bottom:0;' +
                                'width:2px;background:rgba(255,230,0,0.9);' +
                                'pointer-events:none;z-index:10;';
                            tlEl.appendChild(tick);

                            function reposition() {
                                tick.style.left = Math.round(frac * tlEl.offsetWidth) + 'px';
                            }
                            reposition();

                            if (typeof ResizeObserver !== 'undefined') {
                                window._caTickObs = new ResizeObserver(reposition);
                                window._caTickObs.observe(tlEl);
                            }
                        }());
                    };

                    // ── Internal display update (re-runs on frame toggle) ─────────────────
                    window._updateDisplay = function (data) {
                        viewer.entities.removeAll();
                        window._satAEntity = null;
                        window._satBEntity = null;

                        var eci = (window._frame === 'ECI');

                        // Builds a SampledPositionProperty from a flat [x,y,z,…] metre array.
                        // INERTIAL ref-frame in ECI mode: Cesium handles the rotation to ECEF
                        // at render time, giving a correct inertial-frame orbit arc.
                        function buildSampled(flat, startIso) {
                            var sp = new Cesium.SampledPositionProperty(
                                eci ? Cesium.ReferenceFrame.INERTIAL
                                    : Cesium.ReferenceFrame.FIXED);
                            sp.forwardExtrapolationType  = Cesium.ExtrapolationType.HOLD;
                            sp.backwardExtrapolationType = Cesium.ExtrapolationType.HOLD;
                            sp.setInterpolationOptions({
                                interpolationDegree:    5,
                                interpolationAlgorithm: Cesium.LagrangePolynomialApproximation,
                            });
                            var epoch = Cesium.JulianDate.fromIso8601(startIso);
                            var n = flat.length / 3;
                            for (var i = 0; i < n; i++) {
                                var jd  = Cesium.JulianDate.addSeconds(
                                    epoch, i * 60, new Cesium.JulianDate());
                                var pos = new Cesium.Cartesian3(
                                    flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
                                sp.addSample(jd, pos);
                            }
                            return sp;
                        }

                        // Adds one satellite entity with a smooth orbit arc path.
                        // Both ECEF and ECI use the entity path (no static polyline):
                        //   • trailTime/leadTime = full arc duration → entire orbit visible
                        //     at every clock position without a separate polyline.
                        //   • resolution:30 → Cesium queries the Lagrange-interpolated
                        //     SampledPositionProperty every 30 s, producing smooth curves
                        //     between the 1-minute propagation samples.
                        //   • HOLD extrapolation means positions outside the sample window
                        //     collapse to the arc endpoints (zero-length stub, not a spike).
                        function addSat(satData, color) {
                            var flat    = eci ? satData.eciPositions : satData.positions;
                            var arcSecs = (flat.length / 3 - 1) * 60;

                            return viewer.entities.add({
                                position: buildSampled(flat, satData.startIso),
                                point: {
                                    pixelSize: 9,
                                    color: color,
                                    outlineColor: Cesium.Color.BLACK,
                                    outlineWidth: 1,
                                },
                                label: {
                                    text: satData.name,
                                    font: '13px sans-serif',
                                    fillColor: color,
                                    outlineColor: Cesium.Color.BLACK,
                                    outlineWidth: 2,
                                    style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                                    verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                                    pixelOffset: new Cesium.Cartesian2(0, -12),
                                },
                                path: {
                                    resolution: 30,
                                    material: new Cesium.ColorMaterialProperty(color.withAlpha(0.9)),
                                    width: 2,
                                    trailTime: arcSecs,
                                    leadTime:  arcSecs,
                                },
                            });
                        }

                        // Closest-approach marker at Sat A's CA position.
                        // Uses ConstantPositionProperty(INERTIAL) in ECI mode so the marker
                        // stays fixed in the inertial frame while Earth rotates.
                        function addCaMarker(d) {
                            var pos = eci ? d.a.labelPosEci : d.a.labelPos;
                            var c3  = new Cesium.Cartesian3(pos[0], pos[1], pos[2]);
                            viewer.entities.add({
                                position: eci
                                    ? new Cesium.ConstantPositionProperty(
                                        c3, Cesium.ReferenceFrame.INERTIAL)
                                    : c3,
                                point: {
                                    pixelSize: 11,
                                    color: Cesium.Color.YELLOW,
                                    outlineColor: Cesium.Color.BLACK,
                                    outlineWidth: 2,
                                },
                                label: {
                                    text: d.caLabel,
                                    font: '11px sans-serif',
                                    fillColor: Cesium.Color.YELLOW,
                                    outlineColor: Cesium.Color.BLACK,
                                    outlineWidth: 2,
                                    style: Cesium.LabelStyle.FILL_AND_OUTLINE,
                                    verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
                                    pixelOffset: new Cesium.Cartesian2(0, -14),
                                    showBackground: true,
                                    backgroundColor: new Cesium.Color(0, 0, 0.1, 0.55),
                                },
                            });
                        }

                        window._satAEntity = addSat(data.a, Cesium.Color.CYAN);
                        window._satBEntity = addSat(data.b, Cesium.Color.fromCssColorString('#FF8C00'));
                        addCaMarker(data);

                        if (window._tracking && window._satAEntity)
                            viewer.trackedEntity = window._satAEntity;
                    };

                    // Pause playback and jump to a specific time (called from C# on chart click).
                    window.jumpToTime = function (isoString) {
                        viewer.clock.shouldAnimate = false;
                        viewer.clock.currentTime   = Cesium.JulianDate.fromIso8601(isoString);
                    };

                } catch (err) {
                    document.body.style.cssText =
                        'background:#1a1a2e;color:#f88;font-family:monospace;' +
                        'padding:16px;box-sizing:border-box;';
                    document.body.textContent = 'Cesium init error: ' + err;
                }
            }());
            </script>
        </body>
        </html>
        """;
}
