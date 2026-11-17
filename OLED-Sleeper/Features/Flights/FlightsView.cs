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
        private static readonly Color RunwayAreaColor = Color.FromRgb(0x1C, 0x1F, 0x27);
        private static readonly Color RunwayColor = Color.FromRgb(0x3A, 0x3F, 0x4B);
        private static readonly Color AirportCodeColor = Color.FromRgb(0x5E, 0x6B, 0x82);
        private static readonly Color AirportNameColor = Color.FromRgb(0x3E, 0x44, 0x50);
        private static readonly Color BorderColor = Color.FromRgb(0x27, 0x20, 0x30);
        private static readonly Color RiverColor = Color.FromRgb(0x12, 0x2A, 0x45);
        private static readonly Color RailColor = Color.FromRgb(0x29, 0x2E, 0x37);
        private static readonly Color MinorColor = Color.FromRgb(0x2C, 0x2E, 0x32);   // smaller roads: a faint white
        private static readonly Color MajorColor = Color.FromRgb(0x27, 0x26, 0x24);
        private static readonly Color MotorwayColor = Color.FromRgb(0x40, 0x33, 0x1F);
        private static readonly Brush PinHead = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0x2A, 0x2A)));
        private static readonly Brush PinShine = Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xF0, 0x9A, 0x8A)));
        private static readonly Brush PinNeedle = Frozen(new SolidColorBrush(Color.FromRgb(0x78, 0x7C, 0x84)));
        private static readonly Brush PinShadow = Frozen(new SolidColorBrush(Color.FromArgb(0x70, 0x30, 0x34, 0x3C)));

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
        private IDisposable? _tourUse;
        private TourScene _scene;
        /// <summary>Where the view looks (km from the scene centre): the followed plane, or the scene centre.</summary>
        private Point _focus;

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
            _scene = FlightTour.HomeScene(settings);
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
            _tourUse = FlightTour.Attach(PanoramaCount > 1 ? (int)Math.Round(PanoramaIndex) : null);
            AircraftFeed.Updated += OnFeedUpdated;
            Connect(FlightTour.SceneAt(DateTime.UtcNow, _settings));
            CompositionTarget.Rendering += OnRendering;
        }

        /// <summary>Stops the animation and the data updates (the screensaver closes).</summary>
        public void Stop()
        {
            if (!_running) return;
            _running = false;
            CompositionTarget.Rendering -= OnRendering;
            AircraftFeed.Updated -= OnFeedUpdated;
            _feed?.Dispose();
            _feed = null;
            _tourUse?.Dispose();
            _tourUse = null;
            _locationKey = "";
        }

        /// <summary>(Re)connects to the map and the aircraft feed for a scene: the pin, an airport or a flight.</summary>
        private void Connect(TourScene scene)
        {
            if (!_running) return;
            _scene = scene;
            string key = scene.Key;
            if (key == _locationKey) return;
            _locationKey = key;

            var projection = new LocalProjection(scene.Lat, scene.Lon);
            _tracker.Reset(projection);
            _focus = new Point(0, 0);
            _map = null;
            _mapDirty = true;
            var old = _feed;
            _feed = AircraftFeed.Subscribe(scene.Lat, scene.Lon, FeedRadiusKm(scene.RadiusKm));
            old?.Dispose();
            // Planes from a moment ago (fetched during the fade, or while the previous scene was on): at once.
            if (AircraftFeed.RecentFor(scene.Lat, scene.Lon) is { } recent) _tracker.Update(recent, DateTime.UtcNow);

            LoadMap();
        }

        private DateTime _mapAskedUtc;

        /// <summary>Ask for the map of this scene; when it comes, show it (unless the scene moved on).</summary>
        private void LoadMap()
        {
            var scene = _scene;
            string key = _locationKey;
            _mapAskedUtc = DateTime.UtcNow;
            MapLoader.GetAsync(scene.Lat, scene.Lon, scene.RadiusKm, _settings.CacheDirectory)
                .ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion || t.Result == null) return;
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (_locationKey == key && !ReferenceEquals(_map, t.Result)) { _map = t.Result; _mapDirty = true; }
                    });
                });
        }

        /// <summary>How far around the pin to ask for planes: the drifting view plus a margin for planes flying in.</summary>
        public static double FeedRadiusKm(double radiusKm) => radiusKm * 1.6;

        private void OnFeedUpdated() => Dispatcher.BeginInvoke(() =>
        {
            // Only planes for this scene (the tour may already be fetching the next place).
            if (_running && AircraftFeed.RecentFor(_scene.Lat, _scene.Lon) is { } reports) _tracker.Update(reports, DateTime.UtcNow);
        });

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_frameClock.IsRunning && _frameClock.Elapsed.TotalMilliseconds < 1000.0 / MaxFramesPerSecond - 0.5) return;
            _frameClock.Restart();
            var now = DateTime.UtcNow;
            var scene = FlightTour.SceneAt(now, _settings);
            if (scene.Key != _locationKey) Connect(scene);
            // No map yet, or one with gaps (the internet was away): try again now and then.
            else if ((_map == null || !_map.Complete) && now - _mapAskedUtc > MapLoader.RetryAfter) LoadMap();
            _focus = FocusAt(now);
            // Small (the preview in the settings window): the map moves under a pixel per second, 5 redraws are plenty.
            // Following a plane the map slides along quickly: then it is redrawn every frame.
            double mapRate = _scene.Kind == SceneKind.Follow ? MaxFramesPerSecond : ActualWidth < 1200 ? 5 : MapFramesPerSecond;
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

        /// <summary>The followed plane's position (smoothly moving), or the scene centre.</summary>
        private Point FocusAt(DateTime now)
        {
            if (_scene.Kind != SceneKind.Follow) return new Point(0, 0);
            var plane = _tracker.Snapshot(now).FirstOrDefault(p => p.Hex == _scene.FollowHex);
            return plane != null ? new Point(plane.X, plane.Y) : _focus;
        }

        /// <summary>World (km around the scene centre) to this screen's pixels, for this moment.</summary>
        private (Matrix Matrix, double Scale, double RotationDeg) ViewAt(DateTime utc)
        {
            var cam = FlightCamera.At(utc);
            double radius = _scene.RadiusKm;
            double totalWidth = Math.Max(1, TotalWidth);
            double scale = totalWidth / (2 * radius) * cam.Zoom;          // px per km
            double halfHeightKm = radius * ActualHeight / totalWidth;

            // The pin: the whole map drifts around it. An airport drifts less (it stays well in view); a followed
            // plane stays in the middle.
            double drift = _scene.Kind == SceneKind.Home ? 1 : _scene.Kind == SceneKind.Airport ? 0.45 : 0.2;
            double cx = _focus.X + cam.Dx * radius * drift, cy = _focus.Y + cam.Dy * halfHeightKm * drift;
            // The pin is in the middle of all screens together; an airport or plane in the middle of one screen.
            int count = Math.Max(1, PanoramaCount);
            // When only one screen of the row shows the planes, the pin goes in the middle of that screen.
            var active = PanoramaCount > 1 ? FlightTour.ActiveScreens : Array.Empty<int>();
            double targetX = _scene.Kind != SceneKind.Home ? FocusScreen * Stride + ActualWidth / 2
                           : active.Count == 1 ? active[0] * Stride + ActualWidth / 2
                           : totalWidth / 2;
            var m = Matrix.Identity;
            m.Translate(-cx, -cy);
            m.Rotate(cam.RotationDeg);
            m.Scale(scale, scale);
            // An airport or plane is never exactly in the middle: each scene somewhere else, slowly wandering.
            var (offX, offY) = _scene.Kind == SceneKind.Home ? (0.0, 0.0) : SceneOffset(utc);
            m.Translate(targetX + offX - PanoramaIndex * Stride, ActualHeight / 2 + offY);
            return (m, scale, cam.RotationDeg);
        }

        /// <summary>Random per scene (the same on every screen) and fixed for the scene: 0..1 values.</summary>
        private (double A, double B, double C, double D) SceneRandom()
        {
            var r = new Random(_scene.Number * 7919 + (int)(FlightCamera.SessionPhases[0] * 100000));
            return (r.NextDouble(), r.NextDouble(), r.NextDouble(), r.NextDouble());
        }

        /// <summary>Where the airport or plane sits, in pixels from the middle of its screen: a different spot
        /// each scene, gliding in a straight line (its own direction each scene) from start to end.</summary>
        private (double X, double Y) SceneOffset(DateTime utc)
        {
            var (a, b, c, _) = SceneRandom();
            double duration = Math.Max(1, (_scene.EndUtc - _scene.StartUtc).TotalSeconds);
            double along = Math.Clamp((utc - _scene.StartUtc).TotalSeconds / duration, 0, 1) - 0.5; // -0.5 .. 0.5
            double angle = c * 2 * Math.PI, length = 0.14 * ActualWidth;
            double x = (a - 0.5) * 0.42 * ActualWidth + along * length * Math.Cos(angle);
            double y = (b - 0.5) * 0.36 * ActualHeight + along * length * Math.Sin(angle);
            return (x, y);
        }

        /// <summary>The screen the airport or plane of this scene is on (alternating between the screens).</summary>
        private int FocusScreen
        {
            get
            {
                // Only screens that show the planes (one asleep, the other in use: everything on the sleeping one).
                var active = PanoramaCount > 1 ? FlightTour.ActiveScreens : Array.Empty<int>();
                return active.Count > 0 ? active[_scene.FocusScreen % active.Count] : _scene.FocusScreen % Math.Max(1, PanoramaCount);
            }
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
            if (_scene.Kind == SceneKind.Home) DrawPin(dc, m.Transform(new Point(0, 0)), ui, now);
            DrawPlanes(dc, m, rotation, ui, bounds, now);
            // Between two scenes: fade through black.
            double visible = _scene.Visibility(now, FlightTour.FadeSeconds);
            if (visible < 1) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Round((1 - visible) * 255), 0, 0, 0)), null, bounds);
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
                [MapKind.Minor] = PenFor(MinorColor, 0.55),
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

            // Airports: the code, with the short name and the country under it, next to the runways.
            foreach (var airport in _map!.Airports)
            {
                var p = m.Transform(new Point(airport.X, airport.Y));
                if (!outside.Contains(p)) continue;
                var code = Text(airport.Code, CityFace, 12.5 * ui, AirportCodeColor);
                var at = p + new Vector(8 * ui, -code.Height);
                dc.DrawText(code, at);
                string country = AirportCatalog.CountryName(airport.Code) ?? "";
                string line = string.Join(" · ", new[] { airport.Name, country }.Where(x => x.Length > 0));
                if (line.Length > 0)
                    dc.DrawText(Text(line, LabelFace, 10.5 * ui, AirportNameColor), at + new Vector(0, code.Height * 0.92));
            }
        }

        private static void DrawPin(DrawingContext dc, Point p, double ui, DateTime now)
        {
            // Your spot: a pin stuck in the map, its point exactly on the spot (it drifts along with the map).
            double needle = 15 * ui, head = 4.6 * ui;
            var top = p - new Vector(0, needle);
            dc.DrawEllipse(PinShadow, null, p, 2.4 * ui, 1.1 * ui);
            dc.DrawLine(new Pen(PinNeedle, 1.4 * ui) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, p, top);
            dc.DrawEllipse(PinHead, null, top, head, head);
            dc.DrawEllipse(PinShine, null, top + new Vector(-head * 0.35, -head * 0.35), head * 0.3, head * 0.3);
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

                bool followed = _scene.Kind == SceneKind.Follow && plane.Hex == _scene.FollowHex;
                double size = (plane.OnGround ? 6 : 9) * ui * SizeOf(plane.Kind) * (followed ? 1.7 : 1);
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

                if (followed)
                {
                    DrawFollowLabel(dc, plane, p, size, ui);
                    continue;
                }

                if (labels && !plane.OnGround && plane.Callsign.Length > 0)
                {
                    string altitude = plane.AltitudeFeet is { } ft
                        ? (Math.Round(ft * 0.3048 / 100) * 100).ToString("#,0", CultureInfo.GetCultureInfo("nl-NL")) + " m"
                        : "";
                    // Where it flies from and to (when known), else the type of aircraft.
                    string middle = RouteLookup.For(plane.Callsign)?.ToString() ?? plane.TypeCode;
                    string label = string.Join(" · ", new[] { plane.Callsign, middle, altitude }.Where(x => x.Length > 0));
                    var t = Text(label, LabelFace, 10.5 * ui, Color.FromRgb(0x56, 0x61, 0x72));
                    dc.DrawText(t, p + new Vector(size + 5 * ui, size * 0.2));
                }
            }
        }

        /// <summary>The followed flight: call sign and route large, type, height and speed under it.</summary>
        private void DrawFollowLabel(DrawingContext dc, AircraftTracker.Plane plane, Point p, double size, double ui)
        {
            var route = RouteLookup.For(plane.Callsign);
            string title = route != null ? $"{plane.Callsign}   {route}" : plane.Callsign;
            string altitude = plane.AltitudeFeet is { } ft
                ? (Math.Round(ft * 0.3048 / 100) * 100).ToString("#,0", CultureInfo.GetCultureInfo("nl-NL")) + " m"
                : "";
            string speed = _tracker.SpeedKmh(plane.Hex) is { } kmh ? $"{Math.Round(kmh / 10) * 10:0} km/u" : "";
            string details = string.Join(" · ", new[] { plane.TypeCode, altitude, speed }.Where(x => x.Length > 0));
            var t1 = Text(title, CityFace, 17 * ui, Color.FromRgb(0x8A, 0x92, 0xA0));
            var at = p + new Vector(size + 12 * ui, -t1.Height * 0.8);
            dc.DrawText(t1, at);
            if (details.Length > 0) dc.DrawText(Text(details, LabelFace, 12.5 * ui, Color.FromRgb(0x5E, 0x66, 0x74)), at + new Vector(0, t1.Height));
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
