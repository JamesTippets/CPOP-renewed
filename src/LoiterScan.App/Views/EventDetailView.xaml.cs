using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LoiterScan.App.ViewModels;

namespace LoiterScan.App.Views;

public partial class EventDetailView : UserControl
{
    private EventDetailViewModel? _vm;
    private bool _cesiumPageReady;

    public EventDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await CesiumView.EnsureCoreWebView2Async();
            CesiumView.CoreWebView2.NavigationCompleted += OnCesiumNavigationCompleted;
            CesiumView.NavigateToString(CesiumHtml());
        }
        catch { /* WebView2 runtime unavailable — viewer area stays blank */ }
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
        }
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
        }
        RangePlot.Refresh();

        _ = InjectOrbitPathsAsync();
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
                        // The timeline canvas fills its container linearly; a % left value
                        // maps cleanly to clock time without needing pixel math.
                        (function () {
                            var old = document.getElementById('ca-tl-marker');
                            if (old) old.parentNode.removeChild(old);

                            if (!data.caTimeIso) return;
                            var caJd   = Cesium.JulianDate.fromIso8601(data.caTimeIso);
                            var total  = Cesium.JulianDate.secondsDifference(stop, start);
                            var offset = Cesium.JulianDate.secondsDifference(caJd, start);
                            var pct    = Math.max(0, Math.min(100, (offset / total) * 100));

                            var tlEl = viewer.timeline.container;

                            var tick = document.createElement('div');
                            tick.id  = 'ca-tl-marker';
                            tick.style.cssText =
                                'position:absolute;top:0;bottom:0;left:' + pct + '%;' +
                                'width:2px;background:rgba(255,230,0,0.9);' +
                                'pointer-events:none;z-index:10;';
                            tlEl.appendChild(tick);
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
