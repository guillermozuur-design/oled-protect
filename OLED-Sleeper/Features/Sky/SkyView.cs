using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OLED_Sleeper.Features.Sky
{
    /// <summary>
    /// OLED-friendly screensaver showing the real night sky over the Netherlands: the brighter stars, the
    /// best-known constellation figures and names, today's planets and Moon, and now and then a slow shooting
    /// star (from the radiant of the active meteor shower when there is one).
    ///
    /// The sky shown is tonight's sky from 22:00 onwards, sped up so it slowly turns, while the view pans
    /// gently. Everything keeps moving and almost all pixels stay truly black, so nothing can burn in.
    ///
    /// Screens next to each other form one wide panorama. Shooting stars are worked out from the clock alone
    /// (same seed on every screen), so a shooting star that leaves one screen carries on across the next one
    /// without the screens having to talk to each other.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class SkyView : FrameworkElement
    {
        // --- tuning ---
        private const double Latitude = 52.1;          // midden van Nederland
        private const double LongitudeEast = 5.1;
        private const double TimeLapse = 30;           // de hemel draait 30x sneller dan echt
        private const double FieldOfView = 75;         // horizontale beeldhoek per scherm, graden (2 schermen = 150)
        private const double PanDegreesPerSecond = 0.015;
        private const double BaseAltitude = 42;        // kijkhoogte: onderrand ligt dan ~12-24 graden boven de horizon
        private const double AltitudeSwing = 6;        // langzaam op en neer
        private const double FrameIntervalMs = 33;

        // Shooting stars: one chance per second-slot, the same on every screen.
        // Real shooting stars are rare, quick and thin: a streak of half a second to a second that is brightest
        // near its end and leaves a short glowing trail. Now and then a slower, brighter fireball.
        private const double MeteorChancePerSecond = 1.0 / 45;
        private const double ShowerChancePerSecond = 1.0 / 20;
        private const double FireballChance = 0.03;
        private const double MaxMeteorLife = 4;        // seconden, inclusief nagloeien

        /// <summary>Only the well-known figures get lines and a name; the rest stays as plain stars.</summary>
        private static readonly HashSet<string> FamousConstellations = new(StringComparer.Ordinal)
        {
            "UMa", "UMi", "Cas", "Cep", "Ori", "Cyg", "Lyr", "Aql", "Leo", "Gem", "Tau", "Sco",
            "Sgr", "Peg", "And", "Per", "Boo", "CMa", "Aur", "Her", "Vir", "CrB", "Dra"
        };

        private static readonly (Astronomy.Body Body, string Name, Color Color, double Radius)[] Planets =
        {
            // Groter dan echt (dan zouden het puntjes zijn), zodat je ze herkent.
            (Astronomy.Body.Mercury, "Mercurius", Color.FromRgb(215, 205, 190), 3.0),
            (Astronomy.Body.Venus, "Venus", Color.FromRgb(255, 246, 220), 5.0),
            (Astronomy.Body.Mars, "Mars", Color.FromRgb(232, 120, 72), 4.2),
            (Astronomy.Body.Jupiter, "Jupiter", Color.FromRgb(236, 214, 182), 6.5),
            (Astronomy.Body.Saturn, "Saturnus", Color.FromRgb(230, 206, 150), 5.0),
        };

        // Major meteor showers: (from month/day, to month/day, radiant RA, radiant Dec).
        private static readonly (int M1, int D1, int M2, int D2, double Ra, double Dec)[] Showers =
        {
            (1, 1, 1, 6, 230, 49),     // Quadrantiden
            (4, 16, 4, 25, 271, 34),   // Lyriden
            (4, 19, 5, 28, 338, -1),   // Eta Aquariden
            (7, 17, 8, 24, 48, 58),    // Perseiden
            (10, 2, 11, 7, 95, 16),    // Orioniden
            (11, 6, 11, 30, 152, 22),  // Leoniden
            (12, 4, 12, 20, 112, 33),  // Geminiden
        };

        private readonly SkyCatalog _catalog = SkyCatalog.Default;
        private readonly Random _random = new();
        private readonly DispatcherTimer _timer;
        private readonly (double Ra, double Dec)[][] _lines;
        private readonly SkyCatalog.Label[] _labels;

        private (double X, double Y, double Z, int ColorBucket, double Alpha, double Radius, double TwinkleSpeed, double TwinklePhase)[] _stars = Array.Empty<(double, double, double, int, double, double, double, double)>();
        private SolidColorBrush[,] _starBrushes = new SolidColorBrush[0, 0];
        private RadialGradientBrush[] _glowBrushes = Array.Empty<RadialGradientBrush>();
        private Pen _linePen = null!;
        private Brush _labelBrush = null!;
        private readonly Dictionary<string, FormattedText> _labelTexts = new();
        private double _pixelsPerDip = 1;

        private DateTime _bodiesComputedAt = DateTime.MinValue;
        private (string Name, double Ra, double Dec, Color Color, double Radius)[] _bodies = Array.Empty<(string, double, double, Color, double)>();
        private (double Ra, double Dec, double Illuminated, bool Waxing) _moon;
        private (double Ra, double Dec, double Longitude) _sun;
        private DateTime? _stillFrameTime;

        /// <summary>Which screen this is, counted from the left (0, 1, ...). Screens next to each other form one panorama.</summary>
        public double PanoramaIndex { get; set; }

        /// <summary>How many screens wide the panorama is (shooting stars can cross all of them).</summary>
        public int PanoramaCount { get; set; } = 1;

        /// <summary>
        /// Pixels of "hidden sky" between two screens, matching the physical bezels, so lines and shooting
        /// stars continue behind the gap instead of jumping.
        /// </summary>
        public double GapPixels { get; set; }

        private double Stride => ActualWidth + GapPixels;
        private double TotalWidth => ActualWidth * Math.Max(1, PanoramaCount) + GapPixels * (Math.Max(1, PanoramaCount) - 1);

        public SkyView()
        {
            _lines = _catalog.Lines.Where(l => FamousConstellations.Contains(l.Abbreviation)).Select(l => l.Points).ToArray();
            _labels = _catalog.Labels.Where(l => FamousConstellations.Contains(l.Abbreviation)).ToArray();

            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
            _timer.Tick += (_, _) => Tick();
            Loaded += (_, _) => Start();
            Unloaded += (_, _) => _timer.Stop();
        }

        public void Stop() => _timer.Stop();

        /// <summary>
        /// Prepares a single still frame without starting the animation, at a moment with shooting stars in
        /// flight. Used to render a preview image (tests / README); the live screensaver never calls this.
        /// </summary>
        public void PrepareStillFrame()
        {
            _pixelsPerDip = 1;
            BuildBrushes();
            BuildStars();
            ComputeBodies();

            // Find a moment in the next few minutes where a shooting star is about halfway.
            var t = DateTime.UtcNow;
            for (int i = 0; i < 20000; i++, t = t.AddSeconds(0.1))
            {
                if (ActiveMeteors(t).Any(m => m.Age > m.Life * 0.6 && m.Age < m.Life * 0.8)) break;
            }
            _stillFrameTime = t;
            StillFrameMeteorHeads = ActiveMeteors(t)
                .Select(m => m.Start + m.Direction * (m.Speed * Math.Min(m.Age, m.Life)) - new Vector(PanoramaIndex * Stride, 0))
                .ToList();
        }

        /// <summary>Where the shooting stars are in the still frame (this screen's pixels); for preview crops.</summary>
        public IReadOnlyList<Point> StillFrameMeteorHeads { get; private set; } = Array.Empty<Point>();

        // ================= setup =================

        private void Start()
        {
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            BuildBrushes();
            BuildStars();
            _timer.Start();
        }

        private static readonly Color[] StarColors =
        {
            Color.FromRgb(160, 185, 255), // O/B blauw
            Color.FromRgb(205, 218, 255), // A wit-blauw
            Color.FromRgb(248, 247, 255), // F wit
            Color.FromRgb(255, 242, 228), // G geelwit
            Color.FromRgb(255, 214, 170), // K oranje
            Color.FromRgb(255, 185, 135), // M rood-oranje
        };

        private static int ColorBucket(double bv) => bv switch
        {
            < -0.05 => 0,
            < 0.2 => 1,
            < 0.45 => 2,
            < 0.8 => 3,
            < 1.3 => 4,
            _ => 5
        };

        private void BuildBrushes()
        {
            const int alphaLevels = 32;
            _starBrushes = new SolidColorBrush[StarColors.Length, alphaLevels];
            _glowBrushes = new RadialGradientBrush[StarColors.Length];
            for (int c = 0; c < StarColors.Length; c++)
            {
                var col = StarColors[c];
                for (int a = 0; a < alphaLevels; a++)
                {
                    var b = new SolidColorBrush(Color.FromArgb((byte)(255.0 * a / (alphaLevels - 1)), col.R, col.G, col.B));
                    b.Freeze();
                    _starBrushes[c, a] = b;
                }
                var glow = new RadialGradientBrush(Color.FromArgb(90, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B));
                glow.Freeze();
                _glowBrushes[c] = glow;
            }

            _linePen = new Pen(new SolidColorBrush(Color.FromArgb(24, 120, 150, 215)), 0.8);
            _linePen.Freeze();
            _labelBrush = new SolidColorBrush(Color.FromArgb(64, 150, 170, 220));
            _labelBrush.Freeze();
        }

        private void BuildStars()
        {
            _stars = _catalog.Stars.Select(s =>
            {
                double ra = s.Ra * Astronomy.Deg, dec = s.Dec * Astronomy.Deg;
                double alpha = Math.Clamp(1.05 - 0.15 * s.Magnitude, 0.28, 1.0);
                double radius = Math.Clamp(2.9 - 0.42 * s.Magnitude, 0.9, 3.6);
                return (Math.Cos(dec) * Math.Cos(ra), Math.Cos(dec) * Math.Sin(ra), Math.Sin(dec),
                        ColorBucket(s.ColorIndex), alpha, radius,
                        1.2 + _random.NextDouble() * 2.5, _random.NextDouble() * Math.PI * 2);
            }).ToArray();
        }

        // ================= time & camera =================

        /// <summary>Tonight 22:00 local time, as UTC. Shared by all screens so the panorama lines up.</summary>
        private static DateTime TonightUtc()
        {
            var local = DateTime.Now;
            var anchor = local.Date.AddHours(22);
            if (local.Hour < 6) anchor = anchor.AddDays(-1); // na middernacht: nog steeds "vannacht"
            return anchor.ToUniversalTime();
        }

        private static double CurrentLst(DateTime nowUtc)
        {
            double elapsedDays = (nowUtc - DateTime.Today.ToUniversalTime()).TotalDays;
            double d = Astronomy.DaysSinceJ2000(TonightUtc());
            return Astronomy.Normalize(Astronomy.LocalSiderealTime(d, LongitudeEast) + 360.98564736629 * elapsedDays * TimeLapse);
        }

        private readonly struct Camera
        {
            public readonly double Fx, Fy, Fz, Rx, Ry, Ux, Uy, Uz, Scale, Cx, Cy;

            /// <param name="totalWidth">Width of the whole panorama (all screens side by side).</param>
            /// <param name="fieldOfView">Horizontal field of view of the whole panorama, degrees.</param>
            /// <param name="centerX">Where the panorama centre falls in this screen's pixels.</param>
            public Camera(double azimuthDeg, double altitudeDeg, double totalWidth, double fieldOfView, double centerX, double height)
            {
                double a = azimuthDeg * Astronomy.Deg, h = altitudeDeg * Astronomy.Deg;
                Fx = Math.Cos(h) * Math.Sin(a); Fy = Math.Cos(h) * Math.Cos(a); Fz = Math.Sin(h);
                Rx = Math.Cos(a); Ry = -Math.Sin(a);
                Ux = -Math.Sin(h) * Math.Sin(a); Uy = -Math.Sin(h) * Math.Cos(a); Uz = Math.Cos(h);
                Scale = totalWidth / 2 / (2 * Math.Tan(fieldOfView / 4 * Astronomy.Deg));
                Cx = centerX; Cy = height / 2;
            }

            /// <summary>Stereographic projection of an East-North-Up direction. False when it is behind the viewer.</summary>
            public bool Project(double e, double n, double u, out Point p)
            {
                double denom = 1 + e * Fx + n * Fy + u * Fz;
                if (denom < 0.08) { p = default; return false; }
                double x = (e * Rx + n * Ry) / denom;
                double y = (e * Ux + n * Uy + u * Uz) / denom;
                p = new Point(Cx + Scale * 2 * x, Cy - Scale * 2 * y);
                return true;
            }
        }

        private Camera CameraFor(DateTime nowUtc, double panoramaIndex)
        {
            double t = (nowUtc - DateTime.Today.ToUniversalTime()).TotalSeconds;
            double altitude = BaseAltitude + AltitudeSwing * Math.Sin(2 * Math.PI * t / 1200);
            double azimuth = 160 + t * PanDegreesPerSecond;

            // One camera for the whole panorama: all screens side by side form one wide picture and each screen
            // shows its own slice of it. That way the screens join seamlessly, also above and below the middle.
            // The field of view grows with the total width (bezel gaps included), so every pixel covers the same angle.
            double totalWidth = TotalWidth;
            double centerX = totalWidth / 2 - panoramaIndex * Stride;
            return new Camera(azimuth, altitude, totalWidth, FieldOfView * totalWidth / ActualWidth, centerX, ActualHeight);
        }

        // ================= animation =================

        private void Tick()
        {
            if ((DateTime.UtcNow - _bodiesComputedAt).TotalMinutes > 10) ComputeBodies();
            InvalidateVisual();
        }

        private void ComputeBodies()
        {
            double d = Astronomy.DaysSinceJ2000(TonightUtc());
            _bodies = Planets.Select(p =>
            {
                var (ra, dec) = Astronomy.Planet(p.Body, d);
                return (p.Name, ra, dec, p.Color, p.Radius);
            }).ToArray();
            _moon = Astronomy.Moon(d);
            _sun = Astronomy.Sun(d);
            _bodiesComputedAt = DateTime.UtcNow;
        }

        private static (double Ra, double Dec)? ActiveShower()
        {
            var today = DateTime.Today;
            foreach (var s in Showers)
            {
                var from = new DateTime(today.Year, s.M1, s.D1);
                var to = new DateTime(today.Year, s.M2, s.D2);
                if (today >= from && today <= to) return (s.Ra, s.Dec);
            }
            return null;
        }

        // ================= shooting stars (shared by all screens) =================

        /// <summary>A shooting star in panorama coordinates: x runs over all screens side by side.</summary>
        /// <param name="Life">How long the meteor itself burns (s).</param>
        /// <param name="Persistence">How fast the trail fades behind it (s, exponential).</param>
        private readonly record struct Meteor(Point Start, Vector Direction, double Speed, double Life, double Persistence,
                                              double Width, Color Color, double Age);

        /// <summary>
        /// The shooting stars in flight at <paramref name="nowUtc"/>. Each whole second has a fixed, seeded
        /// chance of starting one, so every screen computes exactly the same set.
        /// </summary>
        private IEnumerable<Meteor> ActiveMeteors(DateTime nowUtc)
        {
            if (ActualWidth <= 0) yield break;

            long nowSlot = (long)Math.Floor((nowUtc - DateTime.UnixEpoch).TotalSeconds);
            var shower = ActiveShower();
            double chance = shower != null ? ShowerChancePerSecond : MeteorChancePerSecond;
            double totalWidth = TotalWidth;

            for (long slot = nowSlot - (long)Math.Ceiling(MaxMeteorLife); slot <= nowSlot; slot++)
            {
                var rng = new Random(unchecked((int)(slot * 2654435761L)));
                if (rng.NextDouble() >= chance) continue;

                var startTime = DateTime.UnixEpoch.AddSeconds(slot + rng.NextDouble());
                double age = (nowUtc - startTime).TotalSeconds;

                bool fireball = rng.NextDouble() < FireballChance;
                double life = fireball ? 1.6 + rng.NextDouble() * 0.9 : 0.45 + rng.NextDouble() * 0.6;
                double persistence = fireball ? 0.35 : 0.11;
                if (age < 0 || age >= life + persistence * 4) continue;

                var start = new Point(totalWidth * (0.05 + 0.9 * rng.NextDouble()), ActualHeight * (0.05 + 0.45 * rng.NextDouble()));

                // Direction: away from the shower radiant when it is in view, otherwise a gentle diagonal.
                Vector dir = default;
                bool fromRadiant = false;
                if (shower != null && rng.NextDouble() < 0.75 && RadiantInPanorama(shower.Value, startTime) is Point radiant)
                {
                    dir = start - radiant;
                    fromRadiant = dir.Length > 60;
                }
                if (!fromRadiant)
                {
                    double angle = (12 + rng.NextDouble() * 38) * Astronomy.Deg;
                    dir = new Vector(Math.Cos(angle) * (rng.Next(2) == 0 ? -1 : 1), Math.Sin(angle));
                }
                dir.Normalize();

                double tint = rng.NextDouble();
                var color = tint < 0.6 ? Color.FromRgb(235, 242, 255)      // wit
                          : tint < 0.85 ? Color.FromRgb(200, 255, 215)     // groenig (magnesium)
                          : Color.FromRgb(255, 236, 190);                  // geelachtig (natrium)

                // Path length on screen: normal 250-900 px, fireballs 700-1600 px.
                double distance = fireball ? 700 + rng.NextDouble() * 900 : 250 + rng.NextDouble() * 650;

                yield return new Meteor(start, dir,
                    Speed: distance / life,
                    Life: life,
                    Persistence: persistence,
                    Width: fireball ? 2.6 : 1.3,
                    Color: fireball ? Color.FromRgb(225, 255, 225) : color,
                    Age: age);
            }
        }

        /// <summary>The shower radiant in panorama coordinates, or null when no screen shows it.</summary>
        private Point? RadiantInPanorama((double Ra, double Dec) radiant, DateTime atUtc)
        {
            var (e, n, u) = Astronomy.ToHorizon(radiant.Ra, radiant.Dec, CurrentLst(atUtc), Latitude);
            if (u < -0.1) return null;

            double totalWidth = TotalWidth;
            if (CameraFor(atUtc, 0).Project(e, n, u, out var p) && p.X >= 0 && p.X <= totalWidth) return p;
            return null;
        }

        // ================= rendering =================

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (_stars.Length == 0 || ActualWidth <= 0) return;

            var now = _stillFrameTime ?? DateTime.UtcNow;
            double lst = CurrentLst(now) * Astronomy.Deg;
            double sinPhi = Math.Sin(Latitude * Astronomy.Deg), cosPhi = Math.Cos(Latitude * Astronomy.Deg);
            double cosL = Math.Cos(lst), sinL = Math.Sin(lst);
            var cam = CameraFor(now, PanoramaIndex);
            double seconds = (now - DateTime.Today.ToUniversalTime()).TotalSeconds;

            (double E, double N, double U) ToEnu(double x, double y, double z)
            {
                double cosDcosH = x * cosL + y * sinL;
                double cosDsinH = x * sinL - y * cosL;
                return (-cosDsinH, z * cosPhi - cosDcosH * sinPhi, z * sinPhi + cosDcosH * cosPhi);
            }

            (double E, double N, double U) RaDecToEnu(double ra, double dec)
            {
                double r = ra * Astronomy.Deg, d = dec * Astronomy.Deg;
                return ToEnu(Math.Cos(d) * Math.Cos(r), Math.Cos(d) * Math.Sin(r), Math.Sin(d));
            }

            // Fade out towards the horizon (atmosphere), nothing below it.
            static double HorizonFade(double u) => Math.Clamp((u - 0.01) / 0.08, 0, 1);

            DrawConstellations(dc, cam, RaDecToEnu);
            DrawStars(dc, cam, ToEnu, seconds);
            DrawPlanets(dc, cam, RaDecToEnu);
            DrawMoon(dc, cam, RaDecToEnu);
            DrawMeteors(dc, now);

            void DrawConstellations(DrawingContext ctx, Camera c, Func<double, double, (double E, double N, double U)> conv)
            {
                foreach (var line in _lines)
                {
                    Point? prev = null;
                    foreach (var (ra, dec) in line)
                    {
                        var (e, n, u) = conv(ra, dec);
                        if (u > 0.04 && c.Project(e, n, u, out var p))
                        {
                            if (prev != null && (prev.Value - p).Length < ActualWidth / 3) ctx.DrawLine(_linePen, prev.Value, p);
                            prev = p;
                        }
                        else prev = null;
                    }
                }

                foreach (var label in _labels)
                {
                    var (e, n, u) = conv(label.Ra, label.Dec);
                    if (u < 0.12 || !c.Project(e, n, u, out var p)) continue;
                    var text = LabelText(label.Name, 13, _labelBrush);
                    ctx.DrawText(text, new Point(p.X - text.Width / 2, p.Y - text.Height / 2));
                }
            }

            void DrawStars(DrawingContext ctx, Camera c, Func<double, double, double, (double E, double N, double U)> conv, double t)
            {
                foreach (var s in _stars)
                {
                    var (e, n, u) = conv(s.X, s.Y, s.Z);
                    if (u <= 0.01) continue;
                    if (!c.Project(e, n, u, out var p)) continue;
                    if (p.X < -10 || p.Y < -10 || p.X > ActualWidth + 10 || p.Y > ActualHeight + 10) continue;

                    double fade = HorizonFade(u);
                    // Twinkling (scintillation) is stronger low in the sky, like in reality.
                    double twinkleDepth = 0.12 + 0.35 * (1 - Math.Clamp(u / 0.5, 0, 1));
                    double twinkle = 1 - twinkleDepth * (0.5 + 0.5 * Math.Sin(t * s.TwinkleSpeed + s.TwinklePhase));
                    double alpha = s.Alpha * fade * twinkle;
                    if (alpha < 0.04) continue;

                    if (s.Radius > 2.0)
                    {
                        ctx.PushOpacity(alpha * 0.8);
                        ctx.DrawEllipse(_glowBrushes[s.ColorBucket], null, p, s.Radius * 4.5, s.Radius * 4.5);
                        ctx.Pop();
                    }
                    ctx.DrawEllipse(StarBrush(s.ColorBucket, alpha), null, p, s.Radius, s.Radius);
                }
            }

            void DrawPlanets(DrawingContext ctx, Camera c, Func<double, double, (double E, double N, double U)> conv)
            {
                foreach (var body in _bodies)
                {
                    var (e, n, u) = conv(body.Ra, body.Dec);
                    if (u <= 0.01 || !c.Project(e, n, u, out var p)) continue;
                    double fade = HorizonFade(u);

                    DrawPlanet(ctx, body.Name, p, body.Radius, body.Color, fade);

                    double labelGap = body.Name == "Saturnus" ? body.Radius * 1.2 : body.Radius;
                    var text = LabelText(body.Name, 12, new SolidColorBrush(Color.FromArgb(80, body.Color.R, body.Color.G, body.Color.B)));
                    ctx.DrawText(text, new Point(p.X - text.Width / 2, p.Y + labelGap + 9));
                }
            }

            void DrawMoon(DrawingContext ctx, Camera c, Func<double, double, (double E, double N, double U)> conv)
            {
                if (_bodiesComputedAt == DateTime.MinValue) return;
                var (e, n, u) = conv(_moon.Ra, _moon.Dec);
                if (u <= 0.01 || !c.Project(e, n, u, out var p)) return;

                double fade = HorizonFade(u);
                const double r = 13; // ongeveer de echte grootte bij deze beeldhoek
                var moonColor = Color.FromRgb(235, 232, 220);

                var halo = new RadialGradientBrush(Color.FromArgb((byte)(28 * fade * _moon.Illuminated), moonColor.R, moonColor.G, moonColor.B),
                                                   Color.FromArgb(0, moonColor.R, moonColor.G, moonColor.B));
                halo.Freeze();
                ctx.DrawEllipse(halo, null, p, r * 4, r * 4);

                // The dark part, barely visible (earthshine).
                var dark = new SolidColorBrush(Color.FromArgb((byte)(16 * fade), moonColor.R, moonColor.G, moonColor.B));
                dark.Freeze();
                ctx.DrawEllipse(dark, null, p, r, r);

                // Lit part, turned towards the Sun.
                var (se, sn, su) = conv(_sun.Ra, _sun.Dec);
                double angle;
                if (c.Project(se, sn, su, out var sp)) angle = Math.Atan2(sp.Y - p.Y, sp.X - p.X) / Astronomy.Deg;
                else angle = _moon.Waxing ? 0 : 180;

                var lit = MoonPhaseGeometry(r, _moon.Illuminated);
                var group = new TransformGroup();
                group.Children.Add(new RotateTransform(angle));
                group.Children.Add(new TranslateTransform(p.X, p.Y));
                lit.Transform = group;
                var litBrush = new SolidColorBrush(Color.FromArgb((byte)(150 * fade), moonColor.R, moonColor.G, moonColor.B));
                litBrush.Freeze();
                ctx.DrawGeometry(litBrush, null, lit);

                var text = LabelText("Maan", 12, new SolidColorBrush(Color.FromArgb(60, moonColor.R, moonColor.G, moonColor.B)));
                ctx.DrawText(text, new Point(p.X - text.Width / 2, p.Y + r + 8));
            }

            void DrawMeteors(DrawingContext ctx, DateTime at)
            {
                const int samples = 28;
                double offsetX = PanoramaIndex * Stride; // panorama -> this screen
                var shift = new Vector(offsetX, 0);

                // Brightness while burning: builds up, peaks late, then dies quickly (x = 0..1 of its life).
                static double Burn(double x) => x <= 0 || x >= 1 ? 0 : Math.Pow(Math.Sin(Math.PI * Math.Pow(x, 0.8)), 1.3);

                foreach (var m in ActiveMeteors(at))
                {
                    double headTime = Math.Min(m.Age, m.Life);
                    double oldest = Math.Max(0, headTime - m.Persistence * 4);

                    Point Position(double t) => m.Start + m.Direction * (m.Speed * t) - shift;

                    // Skip quickly when the whole streak is on another screen.
                    var head = Position(headTime);
                    var tail = Position(oldest);
                    if (Math.Max(head.X, tail.X) < -20 || Math.Min(head.X, tail.X) > ActualWidth + 20) continue;

                    // The trail: every point along the path glows as bright as the meteor was when it passed,
                    // fading away exponentially since then. That gives the natural tapering streak.
                    Point prev = head;
                    double prevAlpha = 0;
                    for (int i = 0; i <= samples; i++)
                    {
                        double t = headTime - (headTime - oldest) * i / samples;
                        double alpha = Burn(t / m.Life) * Math.Exp(-(m.Age - t) / m.Persistence);
                        var p = Position(t);
                        if (i > 0)
                        {
                            double a = (alpha + prevAlpha) / 2;
                            if (a > 0.01)
                            {
                                var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(240 * Math.Clamp(a, 0, 1)), m.Color.R, m.Color.G, m.Color.B)),
                                                  Math.Max(0.4, m.Width * (0.35 + 0.65 * Math.Clamp(a, 0, 1))))
                                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                                pen.Freeze();
                                ctx.DrawLine(pen, prev, p);
                            }
                        }
                        prev = p;
                        prevAlpha = alpha;
                    }

                    // Only a tiny bright point at the head while it is still burning (a bit more for a fireball).
                    double headBrightness = m.Age < m.Life ? Burn(m.Age / m.Life) : 0;
                    if (headBrightness > 0.05)
                    {
                        var glow = new RadialGradientBrush(Color.FromArgb((byte)(200 * headBrightness), 255, 255, 255),
                                                           Color.FromArgb(0, m.Color.R, m.Color.G, m.Color.B));
                        glow.Freeze();
                        ctx.DrawEllipse(glow, null, head, m.Width * 2.2, m.Width * 2.2);
                    }
                }
            }
        }

        /// <summary>
        /// A small planet: a shaded little globe with a soft glow, plus what makes each one recognisable —
        /// Saturn's rings, Jupiter's cloud bands, the rusty colour of Mars, the brilliance of Venus.
        /// </summary>
        private static void DrawPlanet(DrawingContext ctx, string name, Point p, double r, Color c, double fade)
        {
            byte A(double a) => (byte)Math.Clamp(a * fade, 0, 255);
            static Color Shade(Color col, double f) => Color.FromRgb((byte)(col.R * f), (byte)(col.G * f), (byte)(col.B * f));

            // Glow around it; Venus is by far the brightest.
            double glowSize = name == "Venus" ? 7 : 4.5;
            var glow = new RadialGradientBrush(Color.FromArgb(A(name == "Venus" ? 110 : 70), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
            glow.Freeze();
            ctx.DrawEllipse(glow, null, p, r * glowSize, r * glowSize);

            // Saturn's rings: tilted ellipse, back half behind the planet, front half in front of it.
            var ringTilt = new RotateTransform(-18, p.X, p.Y);
            var ringOuter = new EllipseGeometry(p, r * 2.3, r * 0.85) { Transform = ringTilt };
            var ringInner = new EllipseGeometry(p, r * 1.45, r * 0.52) { Transform = ringTilt };
            var ring = new CombinedGeometry(GeometryCombineMode.Exclude, ringOuter, ringInner);
            var ringBrush = new SolidColorBrush(Color.FromArgb(A(170), 215, 195, 150));
            ringBrush.Freeze();
            var backHalf = new RectangleGeometry(new Rect(p.X - r * 3, p.Y - r * 3, r * 6, r * 3)) { Transform = ringTilt };
            var frontHalf = new RectangleGeometry(new Rect(p.X - r * 3, p.Y, r * 6, r * 3)) { Transform = ringTilt };
            if (name == "Saturnus")
                ctx.DrawGeometry(ringBrush, null, new CombinedGeometry(GeometryCombineMode.Intersect, ring, backHalf));

            // The globe, lit from the upper left.
            var globe = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.3),
                Center = new Point(0.4, 0.35),
                RadiusX = 0.75,
                RadiusY = 0.75
            };
            var bright = name == "Mars" ? Color.FromRgb(250, 160, 110) : Color.FromRgb((byte)Math.Min(255, c.R + 20), (byte)Math.Min(255, c.G + 20), (byte)Math.Min(255, c.B + 20));
            globe.GradientStops.Add(new GradientStop(Color.FromArgb(A(255), bright.R, bright.G, bright.B), 0));
            globe.GradientStops.Add(new GradientStop(Color.FromArgb(A(245), c.R, c.G, c.B), 0.55));
            var dark = Shade(c, 0.45);
            globe.GradientStops.Add(new GradientStop(Color.FromArgb(A(235), dark.R, dark.G, dark.B), 1));
            globe.Freeze();
            ctx.DrawEllipse(globe, null, p, r, r);

            if (name == "Jupiter")
            {
                // Two faint brownish cloud bands across the disc.
                ctx.PushClip(new EllipseGeometry(p, r, r));
                var band = new SolidColorBrush(Color.FromArgb(A(95), 150, 105, 70));
                band.Freeze();
                ctx.DrawRectangle(band, null, new Rect(p.X - r, p.Y - r * 0.38, r * 2, r * 0.22));
                ctx.DrawRectangle(band, null, new Rect(p.X - r, p.Y + r * 0.18, r * 2, r * 0.26));
                ctx.Pop();
            }
            else if (name == "Mars")
            {
                // A small white polar cap.
                var cap = new SolidColorBrush(Color.FromArgb(A(150), 255, 245, 240));
                cap.Freeze();
                ctx.DrawEllipse(cap, null, new Point(p.X, p.Y - r * 0.78), r * 0.35, r * 0.16);
            }
            else if (name == "Saturnus")
            {
                ctx.DrawGeometry(ringBrush, null, new CombinedGeometry(GeometryCombineMode.Intersect, ring, frontHalf));
            }
        }

        /// <summary>
        /// The lit part of the Moon for a given illuminated fraction, lit side pointing along +X, centred on 0,0.
        /// </summary>
        private static Geometry MoonPhaseGeometry(double r, double illuminated)
        {
            var disc = new EllipseGeometry(new Point(0, 0), r, r);
            var rightHalf = new CombinedGeometry(GeometryCombineMode.Intersect, disc, new RectangleGeometry(new Rect(0, -r, r, 2 * r)));
            double k = Math.Clamp(illuminated, 0, 1);
            var terminator = new EllipseGeometry(new Point(0, 0), r * Math.Abs(1 - 2 * k), r);
            return k <= 0.5
                ? new CombinedGeometry(GeometryCombineMode.Exclude, rightHalf, terminator)
                : new CombinedGeometry(GeometryCombineMode.Union, rightHalf, terminator);
        }

        private SolidColorBrush StarBrush(int colorBucket, double alpha)
        {
            int levels = _starBrushes.GetLength(1);
            return _starBrushes[colorBucket, (int)Math.Round(Math.Clamp(alpha, 0, 1) * (levels - 1))];
        }

        private FormattedText LabelText(string text, double size, Brush brush)
        {
            string key = text + "|" + size;
            if (brush == _labelBrush && _labelTexts.TryGetValue(key, out var cached)) return cached;

            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Bahnschrift Light"), size, brush, _pixelsPerDip);
            if (brush == _labelBrush) _labelTexts[key] = formatted;
            return formatted;
        }
    }
}
