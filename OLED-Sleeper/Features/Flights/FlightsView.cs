using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace OLED_Sleeper.Features.Flights
{
    /// <summary>
    /// The "Vliegtuigen" screensaver: a dim line map (roads, rail, water, places) around a pin, with the real
    /// aircraft flying over it. Everything drifts slowly (see <see cref="FlightCamera"/>) so nothing burns in.
    /// Screens next to each other show one continuous map, with the bezel gap taken into account.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class FlightsView : FrameworkElement
    {
        /// <summary>Planes, trails and the pin: in step with the display (smooth on 144/240 Hz), up to this rate.</summary>
        private const double MaxFramesPerSecond = 120;

        /// <summary>The map itself drifts only a few pixels per second: redrawing it 20 times a second moves it
        /// in quarter-pixel steps (invisible) at a fraction of the cost.</summary>
        private const double MapFramesPerSecond = 20;

        // Night-map colours: dim and opaque on black (opaque, so overlapping tile edges never show).
        // Kept dim on purpose: on black an OLED shows even these clearly, and less light means less wear.
        private static readonly Color WaterColor = Color.FromRgb(0x10, 0x22, 0x38);   // coast and big lakes
        private static readonly Color RunwayAreaColor = Color.FromRgb(0x17, 0x19, 0x1F);
        private static readonly Color RunwayColor = Color.FromRgb(0x29, 0x2C, 0x34);
        private static readonly Color BorderColor = Color.FromRgb(0x27, 0x20, 0x30);
        private static readonly Color RiverColor = Color.FromRgb(0x12, 0x2A, 0x45);
        private static readonly Color RailColor = Color.FromRgb(0x29, 0x2E, 0x37);
        private static readonly Color MinorColor = Color.FromRgb(0x23, 0x25, 0x29);
        private static readonly Color MajorColor = Color.FromRgb(0x27, 0x26, 0x24);
        private static readonly Color MotorwayColor = Color.FromRgb(0x40, 0x33, 0x1F);
        private static readonly Brush PinDot = Frozen(new SolidColorBrush(Color.FromRgb(0x5A, 0x7C, 0xC8)));
        private static readonly Pen PinRing = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x2A, 0x3A, 0x5E)), 1.0));

        private static readonly Typeface LabelFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface CityFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        /// <summary>Our own aircraft symbols (seen from above, nose up, about 2 units long), one per sort of
        /// aircraft, so you can tell a jumbo from a regional prop or a helicopter at a glance.</summary>
        private static readonly Dictionary<AircraftKind, Geometry> Shapes = new()
        {
            [AircraftKind.Narrowbody] = Jet(new (double, double)[]
            {
                (0.00, -1.00), (0.05, -0.96), (0.085, -0.86), (0.09, -0.25), (0.93, 0.20), (0.93, 0.29),
                (0.09, 0.08), (0.075, 0.62), (0.40, 0.84), (0.40, 0.92), (0.05, 0.86), (0.02, 0.99), (0.00, 1.00)
            }, (0.36, -0.10, 0.055, 0.20)),
            [AircraftKind.Widebody] = Jet(new (double, double)[]
            {
                (0.00, -1.00), (0.07, -0.95), (0.12, -0.83), (0.125, -0.24), (1.00, 0.25), (1.00, 0.35),
                (0.125, 0.10), (0.10, 0.62), (0.45, 0.84), (0.45, 0.93), (0.07, 0.86), (0.03, 0.99), (0.00, 1.00)
            }, (0.40, -0.12, 0.075, 0.24)),
            [AircraftKind.Jumbo] = Jet(new (double, double)[]
            {
                (0.00, -1.00), (0.08, -0.95), (0.13, -0.80), (0.13, -0.25), (1.00, 0.27), (1.00, 0.37),
                (0.13, 0.10), (0.11, 0.62), (0.46, 0.84), (0.46, 0.93), (0.07, 0.86), (0.03, 0.99), (0.00, 1.00)
            }, (0.33, -0.12, 0.06, 0.22), (0.66, 0.06, 0.06, 0.20)),
            [AircraftKind.Regional] = Jet(new (double, double)[]
            {
                (0.00, -1.00), (0.05, -0.95), (0.08, -0.84), (0.08, -0.32), (0.95, -0.27), (0.95, -0.12),
                (0.08, -0.10), (0.06, 0.66), (0.36, 0.80), (0.36, 0.90), (0.04, 0.88), (0.02, 0.99), (0.00, 1.00)
            }, (0.32, -0.46, 0.05, 0.36)),
            [AircraftKind.Light] = Jet(new (double, double)[]
            {
                (0.00, -1.00), (0.07, -0.95), (0.10, -0.70), (0.10, -0.56), (1.00, -0.53), (1.00, -0.36),
                (0.10, -0.33), (0.04, 0.60), (0.36, 0.70), (0.36, 0.82), (0.03, 0.84), (0.00, 0.90)
            }),
            [AircraftKind.Helicopter] = Helicopter(),
        };

        /// <summary>The rotor disc of a helicopter, on top of its body.</summary>
        private static readonly Geometry RotorShape = Rotor();

        /// <summary>Relative size of each sort of aircraft.</summary>
        private static double SizeOf(AircraftKind kind) => kind switch
        {
            AircraftKind.Jumbo => 1.35,
            AircraftKind.Widebody => 1.2,
            AircraftKind.Regional => 0.85,
            AircraftKind.Light => 0.65,
            AircraftKind.Helicopter => 0.75,
            _ => 1.0
        };

        private static readonly Brush PlaneFill = Frozen(new SolidColorBrush(Color.FromRgb(0xE9, 0xB8, 0x2C)));
        private static readonly Brush PlaneLowFill = Frozen(new SolidColorBrush(Color.FromRgb(0xF0, 0x8A, 0x3C)));
        private static readonly Brush GroundFill = Frozen(new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70)));
        private static readonly Pen PlaneOutline = Frozen(new Pen(new SolidColorBrush(Color.FromRgb(0x14, 0x12, 0x0C)), 0.07));
        private static readonly Brush RotorFill = Frozen(new SolidColorBrush(Color.FromArgb(0x30, 0xE9, 0xB8, 0x2C)));
        private static readonly Pen RotorRim = Frozen(new Pen(new SolidColorBrush(Color.FromArgb(0x70, 0xE9, 0xB8, 0x2C)), 0.05));

        private static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

        /// <summary>A symmetric plane: the right half of the outline (nose to tail) is mirrored; engines are
        /// (x, top, half width, length) pods under both wings.</summary>
        private static Geometry Jet((double X, double Y)[] half, params (double X, double Top, double HalfWidth, double Length)[] engines)
        {
            var outline = half.Skip(1).Select(p => new Point(p.X, p.Y))
                .Concat(half.Reverse().Skip(1).SkipLast(1).Select(p => new Point(-p.X, p.Y)))
                .ToList();
            var g = new StreamGeometry { FillRule = FillRule.Nonzero };
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(half[0].X, half[0].Y), true, true);
                ctx.PolyLineTo(outline, true, true);
                foreach (var e in engines)
                {
                    foreach (double side in new[] { 1.0, -1.0 })
                    {
                        double x = e.X * side;
                        ctx.BeginFigure(new Point(x - e.HalfWidth, e.Top), true, true);
                        ctx.PolyLineTo(new[] { new Point(x + e.HalfWidth, e.Top), new Point(x + e.HalfWidth, e.Top + e.Length), new Point(x - e.HalfWidth, e.Top + e.Length) }, true, true);
                    }
                }
            }
            return Frozen(g);
        }

        /// <summary>Where the rotor mast is on the helicopter symbol (the middle of the cabin).</summary>
        private static readonly Point RotorHub = new(0, -0.35);

        private static Geometry Helicopter()
        {
            // One merged outline (cabin + tail boom + tail fin), so the border does not run through the body.
            Geometry body = new EllipseGeometry(RotorHub, 0.26, 0.44);
            body = Geometry.Combine(body, new RectangleGeometry(new Rect(-0.05, 0.0, 0.10, 0.92), 0.04, 0.04), GeometryCombineMode.Union, null);
            body = Geometry.Combine(body, new RectangleGeometry(new Rect(-0.24, 0.80, 0.48, 0.09), 0.04, 0.04), GeometryCombineMode.Union, null);
            return Frozen(body);
        }

        private static Geometry Rotor() => Frozen(new EllipseGeometry(RotorHub, 0.92, 0.92));

        private readonly FlightsSettings _settings;
        private readonly Stopwatch _frameClock = new();
        private readonly Dictionary<string, FormattedText> _textCache = new();
        private AircraftTracker _tracker;
        private MapGeometry? _map;
        private IDisposable? _feed;
        private bool _running;
        private double _pixelsPerDip = 1;
        private string _locationKey = "";

        // Two layers: the map (redrawn now and then) and the live layer with planes and pin (every frame).
        private readonly DrawingVisual _mapLayer = new();
        private readonly DrawingVisual _liveLayer = new();
        private readonly Stopwatch _mapClock = new();
        private bool _mapDirty = true;

        public FlightsView(FlightsSettings settings)
        {
            _settings = settings;
            // The map is kept as a ready-made picture between its redraws, so the planes moving over it every
            // frame cost almost nothing (also in the settings window, which is drawn in software).
            _mapLayer.CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = false };
            AddVisualChild(_mapLayer);
            AddVisualChild(_liveLayer);
            SizeChanged += (_, _) => _mapDirty = true;
            _tracker = new AircraftTracker(new LocalProjection(settings.Latitude, settings.Longitude));
            Loaded += (_, _) => Start();
            Unloaded += (_, _) => Stop();
        }

        /// <summary>Which screen this is, counted from the left. Screens next to each other form one map.</summary>
        public double PanoramaIndex { get; set; }

        /// <summary>How many screens wide the map is.</summary>
        public int PanoramaCount { get; set; } = 1;

        /// <summary>The bezel gap as a fraction of the screen width.</summary>
        public double GapFraction { get; set; }

        /// <summary>Fill the background black (for the preview in the settings window).</summary>
        public bool DrawBackground { get; set; }

        private double Gap => GapFraction * ActualWidth;
        private double Stride => ActualWidth + Gap;
        private double TotalWidth => ActualWidth * Math.Max(1, PanoramaCount) + Gap * (Math.Max(1, PanoramaCount) - 1);

        /// <summary>Size of text and symbols, relative to a 1080-pixel-high screen.</summary>
        private double Ui => Math.Clamp(ActualHeight / 1080, 0.35, 1.6);

        private void Start()
        {
            if (_running) return;
            _running = true;
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            _settings.Changed += OnSettingsChanged;
            AircraftFeed.Updated += OnFeedUpdated;
            Connect();
            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>Stops the animation and the data updates (the screensaver closes).</summary>
        public void Stop()
        {
            if (!_running) return;
            _running = false;
            CompositionTarget.Rendering -= OnRendering;
            _settings.Changed -= OnSettingsChanged;
            AircraftFeed.Updated -= OnFeedUpdated;
            _feed?.Dispose();
            _feed = null;
        }

        private void OnSettingsChanged() => Dispatcher.BeginInvoke(Connect);

        /// <summary>(Re)connects to the map and the aircraft feed for the current pin.</summary>
        private void Connect()
        {
            if (!_running) return;
            string key = FormattableString.Invariant($"{_settings.Latitude},{_settings.Longitude},{_settings.RadiusKm}");
            if (key == _locationKey) return;
            _locationKey = key;

            var projection = new LocalProjection(_settings.Latitude, _settings.Longitude);
            _tracker.Reset(projection);
            _map = null;
            _mapDirty = true;
            _feed?.Dispose();
            _feed = AircraftFeed.Subscribe(_settings.Latitude, _settings.Longitude, _settings.RadiusKm * 1.8);
            if (AircraftFeed.Latest is { } latest) _tracker.Update(latest.Reports, DateTime.UtcNow);

            MapLoader.GetAsync(_settings.Latitude, _settings.Longitude, _settings.RadiusKm, _settings.CacheDirectory)
                .ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion || t.Result == null) return;
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (_locationKey == key) { _map = t.Result; _mapDirty = true; }
                    });
                });
        }

        private void OnFeedUpdated()
        {
            if (AircraftFeed.Latest is not { } latest) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (_running) _tracker.Update(latest.Reports, DateTime.UtcNow);
            });
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_frameClock.IsRunning && _frameClock.Elapsed.TotalMilliseconds < 1000.0 / MaxFramesPerSecond - 0.5) return;
            _frameClock.Restart();
            var now = DateTime.UtcNow;
            // Small (the preview in the settings window): the map moves under a pixel per second, 5 redraws are plenty.
            double mapRate = ActualWidth < 1200 ? 5 : MapFramesPerSecond;
            if (_mapDirty || !_mapClock.IsRunning || _mapClock.Elapsed.TotalMilliseconds >= 1000.0 / mapRate - 0.5)
            {
                _mapClock.Restart();
                _mapDirty = false;
                RenderMap(now);
            }
            RenderLive(now);
        }

        protected override int VisualChildrenCount => 2;

        protected override Visual GetVisualChild(int index) => index == 0 ? _mapLayer : _liveLayer;

        // ================= drawing =================

        /// <summary>World (km around the pin) to this screen's pixels, for this moment.</summary>
        private (Matrix Matrix, double Scale, double RotationDeg) ViewAt(DateTime utc)
        {
            var cam = FlightCamera.At(utc);
            double radius = _settings.RadiusKm;
            double totalWidth = Math.Max(1, TotalWidth);
            double scale = totalWidth / (2 * radius) * cam.Zoom;          // px per km
            double halfHeightKm = radius * ActualHeight / totalWidth;

            double cx = cam.Dx * radius, cy = cam.Dy * halfHeightKm;      // view centre, km from the pin
            var m = Matrix.Identity;
            m.Translate(-cx, -cy);
            m.Rotate(cam.RotationDeg);
            m.Scale(scale, scale);
            m.Translate(totalWidth / 2 - PanoramaIndex * Stride, ActualHeight / 2);
            return (m, scale, cam.RotationDeg);
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (DrawBackground && ActualWidth > 0) dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
        }

        private void RenderMap(DateTime now)
        {
            using var dc = _mapLayer.RenderOpen();
            if (ActualWidth <= 0 || ActualHeight <= 0 || _map == null) return;
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            var (m, scale, _) = ViewAt(now);
            dc.PushClip(new RectangleGeometry(bounds));
            DrawMap(dc, m, scale, Ui, bounds);
            dc.Pop();
        }

        private void RenderLive(DateTime now)
        {
            using var dc = _liveLayer.RenderOpen();
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            var (m, _, rotation) = ViewAt(now);
            double ui = Ui;
            dc.PushClip(new RectangleGeometry(bounds));
            DrawPin(dc, m.Transform(new Point(0, 0)), ui, now);
            DrawPlanes(dc, m, rotation, ui, bounds, now);
            if (_map == null && AircraftFeed.LastRequestFailed)
            {
                var text = Text("Geen verbinding: kaart en vliegtuigen zijn nu niet beschikbaar", LabelFace, 12 * ui, Color.FromRgb(0x50, 0x56, 0x60));
                dc.DrawText(text, m.Transform(new Point(0, 0)) + new Vector(-text.Width / 2, 24 * ui));
            }
            dc.Pop();
        }

        private void DrawMap(DrawingContext dc, Matrix m, double scale, double ui, Rect bounds)
        {
            Pen PenFor(Color c, double px, DashStyle? dash = null)
            {
                var pen = new Pen(new SolidColorBrush(c), px * ui / scale) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                if (dash != null) pen.DashStyle = dash;
                pen.Freeze();
                return pen;
            }

            // Only the chunks that are (partly) on this screen.
            var visible = new Rect(bounds.X - 4, bounds.Y - 4, bounds.Width + 8, bounds.Height + 8);
            var pens = new Dictionary<MapKind, Pen>
            {
                [MapKind.Water] = PenFor(WaterColor, 1.0),
                [MapKind.Border] = PenFor(BorderColor, 1.2, new DashStyle(new double[] { 4, 3 }, 0)),
                [MapKind.River] = PenFor(RiverColor, 1.3),
                [MapKind.Rail] = PenFor(RailColor, 0.9),
                [MapKind.Minor] = PenFor(MinorColor, 0.8),
                [MapKind.Major] = PenFor(MajorColor, 1.15),
                [MapKind.Motorway] = PenFor(MotorwayColor, 1.7),
                [MapKind.Runway] = PenFor(RunwayColor, 2.4),
            };
            var fills = new Dictionary<MapKind, Brush>
            {
                [MapKind.RunwayArea] = Frozen(new SolidColorBrush(RunwayAreaColor)),
            };
            MapKind[] order = { MapKind.Water, MapKind.RunwayArea, MapKind.Border, MapKind.River, MapKind.Rail, MapKind.Minor, MapKind.Major, MapKind.Motorway, MapKind.Runway };

            dc.PushTransform(new MatrixTransform(m));
            foreach (var kind in order)
            {
                foreach (var chunk in _map!.Chunks)
                {
                    if (chunk.Kind != kind) continue;
                    var onScreen = Rect.Transform(chunk.Bounds, m);
                    if (!onScreen.IntersectsWith(visible)) continue;
                    if (fills.TryGetValue(kind, out var fill)) dc.DrawGeometry(fill, null, chunk.Geometry);
                    else dc.DrawGeometry(null, pens[kind], chunk.Geometry);
                }
            }
            dc.Pop();

            // Place names stay upright and readable; only the ones on this screen are drawn.
            var outside = bounds;
            outside.Inflate(150 * ui, 40 * ui);
            var townBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3E, 0x45));
            foreach (var place in _map!.Places)
            {
                var p = m.Transform(new Point(place.X, place.Y));
                if (!outside.Contains(p)) continue;
                switch (place.Kind)
                {
                    case PlaceKind.Village:
                        break; // too many small dots: villages are left out
                    case PlaceKind.Town:
                        {
                            dc.DrawEllipse(townBrush, null, p, 1.9 * ui, 1.9 * ui);
                            var t = Text(place.Name, LabelFace, 11.5 * ui, Color.FromRgb(0x42, 0x46, 0x4D));
                            dc.DrawText(t, p + new Vector(5 * ui, -t.Height / 2));
                            break;
                        }
                    case PlaceKind.City:
                        {
                            var t = Text(place.Name, CityFace, 15 * ui, Color.FromRgb(0x5A, 0x5F, 0x68));
                            dc.DrawText(t, p - new Vector(t.Width / 2, t.Height / 2));
                            break;
                        }
                }
            }
        }

        private static void DrawPin(DrawingContext dc, Point p, double ui, DateTime now)
        {
            // Your spot: a small, calm dot with a thin ring (it drifts along with the map).
            dc.DrawEllipse(null, PinRing, p, 5.5 * ui, 5.5 * ui);
            dc.DrawEllipse(PinDot, null, p, 2.2 * ui, 2.2 * ui);
        }

        private void DrawPlanes(DrawingContext dc, Matrix m, double rotation, double ui, Rect bounds, DateTime now)
        {
            var outside = bounds;
            outside.Inflate(60 * ui, 60 * ui);
            bool labels = _settings.ShowLabels;

            foreach (var plane in _tracker.Snapshot(now))
            {
                var p = m.Transform(new Point(plane.X, plane.Y));
                if (!outside.Contains(p)) continue;

                double size = (plane.OnGround ? 6 : 9) * ui * SizeOf(plane.Kind);
                var tf = new TransformGroup();
                tf.Children.Add(new ScaleTransform(size, size));
                tf.Children.Add(new RotateTransform(plane.HeadingDeg + rotation));
                tf.Children.Add(new TranslateTransform(p.X, p.Y));
                dc.PushTransform(tf);
                var fill = plane.OnGround ? GroundFill : (plane.AltitudeFeet ?? 30000) < 5000 && plane.Kind != AircraftKind.Helicopter ? PlaneLowFill : PlaneFill;
                dc.DrawGeometry(fill, PlaneOutline, Shapes[plane.Kind]);
                if (plane.Kind == AircraftKind.Helicopter && !plane.OnGround)
                {
                    // A spinning rotor looks like a faint disc; drawn still (thin turning blades this small flicker).
                    dc.DrawGeometry(RotorFill, RotorRim, RotorShape);
                }
                dc.Pop();

                if (labels && !plane.OnGround && plane.Callsign.Length > 0)
                {
                    string altitude = plane.AltitudeFeet is { } ft
                        ? (Math.Round(ft * 0.3048 / 100) * 100).ToString("#,0", CultureInfo.GetCultureInfo("nl-NL")) + " m"
                        : "";
                    string label = string.Join(" · ", new[] { plane.Callsign, plane.TypeCode, altitude }.Where(x => x.Length > 0));
                    var t = Text(label, LabelFace, 10.5 * ui, Color.FromRgb(0x56, 0x61, 0x72));
                    dc.DrawText(t, p + new Vector(size + 5 * ui, size * 0.2));
                }
            }
        }

        private FormattedText Text(string text, Typeface face, double size, Color color)
        {
            string key = FormattableString.Invariant($"{text}|{face.Weight}|{size:0.0}|{color}");
            if (_textCache.TryGetValue(key, out var cached)) return cached;
            if (_textCache.Count > 2000) _textCache.Clear();
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo("nl-NL"), FlowDirection.LeftToRight, face, size, brush, _pixelsPerDip);
            _textCache[key] = ft;
            return ft;
        }
    }
}
