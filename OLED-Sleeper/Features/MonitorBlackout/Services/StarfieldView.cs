using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    /// <summary>
    /// OLED-vriendelijke screensaver: een zwarte hemel met zacht twinkelende sterren die langzaam
    /// omlaag drijven, en af en toe een vallende ster. Bijna alle pixels blijven echt zwart (uit) en
    /// niets staat lang op dezelfde plek, dus er kan niets inbranden.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class StarfieldView : FrameworkElement
    {
        private const double FrameIntervalMs = 33; // ~30 fps is ruim genoeg en spaart de GPU
        private const int AlphaLevels = 32;
        private const double PixelsPerStar = 9000;

        private static readonly Color StarColor = Color.FromRgb(225, 232, 255);
        private static readonly SolidColorBrush[] StarBrushes = CreateBrushes();

        private readonly Random _random = new();
        private readonly List<Star> _stars = new();
        private readonly List<Meteor> _meteors = new();
        private readonly DispatcherTimer _timer;
        private DateTime _lastTick;
        private double _nextMeteorIn;

        public StarfieldView()
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(FrameIntervalMs) };
            _timer.Tick += (_, _) => Tick();
            Loaded += (_, _) => Start();
            Unloaded += (_, _) => _timer.Stop();
            SizeChanged += (_, _) => Populate();
        }

        public void Stop() => _timer.Stop();

        private void Start()
        {
            Populate();
            _lastTick = DateTime.UtcNow;
            _nextMeteorIn = 2 + _random.NextDouble() * 3;
            _timer.Start();
        }

        private void Populate()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            int wanted = (int)(ActualWidth * ActualHeight / PixelsPerStar);
            _stars.Clear();
            for (int i = 0; i < wanted; i++)
            {
                var star = NewStar();
                star.Age = _random.NextDouble() * star.Life; // niet allemaal tegelijk opkomen
                _stars.Add(star);
            }
        }

        private Star NewStar()
        {
            double depth = _random.NextDouble(); // 0 = ver weg, 1 = dichtbij
            return new Star
            {
                X = _random.NextDouble() * ActualWidth,
                Y = _random.NextDouble() * ActualHeight,
                Radius = 0.5 + depth * 1.1,
                Brightness = 0.25 + depth * 0.55,
                Drift = 2 + depth * 7,                      // px/s omlaag
                TwinkleSpeed = 0.6 + _random.NextDouble() * 1.8,
                TwinklePhase = _random.NextDouble() * Math.PI * 2,
                Life = 25 + _random.NextDouble() * 50,     // seconden
                Age = 0
            };
        }

        private void Tick()
        {
            var now = DateTime.UtcNow;
            double dt = Math.Min((now - _lastTick).TotalSeconds, 0.25);
            _lastTick = now;

            for (int i = 0; i < _stars.Count; i++)
            {
                var s = _stars[i];
                s.Age += dt;
                s.Y += s.Drift * dt;
                s.TwinklePhase += s.TwinkleSpeed * dt;
                bool fellOff = s.Y > ActualHeight + 4;
                if (fellOff || s.Age >= s.Life)
                {
                    s = NewStar();
                    if (fellOff) s.Y = -4; // bovenaan weer binnenkomen
                }
                _stars[i] = s;
            }

            _nextMeteorIn -= dt;
            if (_nextMeteorIn <= 0)
            {
                _meteors.Add(NewMeteor());
                _nextMeteorIn = 3 + _random.NextDouble() * 6;
            }

            for (int i = _meteors.Count - 1; i >= 0; i--)
            {
                var m = _meteors[i];
                m.Age += dt;
                m.X += m.Vx * dt;
                m.Y += m.Vy * dt;
                if (m.Age >= m.Life) _meteors.RemoveAt(i);
                else _meteors[i] = m;
            }

            InvalidateVisual();
        }

        private Meteor NewMeteor()
        {
            bool toLeft = _random.Next(2) == 0;
            double angle = (18 + _random.NextDouble() * 22) * Math.PI / 180;
            double speed = 700 + _random.NextDouble() * 600;
            return new Meteor
            {
                X = _random.NextDouble() * ActualWidth,
                Y = _random.NextDouble() * ActualHeight * 0.45,
                Vx = Math.Cos(angle) * speed * (toLeft ? -1 : 1),
                Vy = Math.Sin(angle) * speed,
                Length = 140 + _random.NextDouble() * 160,
                Life = 0.7 + _random.NextDouble() * 0.6,
                Age = 0
            };
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

            foreach (var s in _stars)
            {
                // Rustig op- en afkomen aan begin en eind van het leven, plus twinkelen.
                double fade = Math.Min(1, Math.Min(s.Age / 4, (s.Life - s.Age) / 4));
                double twinkle = 0.65 + 0.35 * Math.Sin(s.TwinklePhase);
                double alpha = Math.Clamp(s.Brightness * twinkle * fade, 0, 1);
                if (alpha < 0.03) continue;
                dc.DrawEllipse(BrushFor(alpha), null, new Point(s.X, s.Y), s.Radius, s.Radius);
            }

            foreach (var m in _meteors)
            {
                double t = m.Age / m.Life;
                double intensity = Math.Sin(Math.PI * t); // opkomen en uitdoven
                double speed = Math.Sqrt(m.Vx * m.Vx + m.Vy * m.Vy);
                var head = new Point(m.X, m.Y);
                var tail = new Point(m.X - m.Vx / speed * m.Length, m.Y - m.Vy / speed * m.Length);

                var gradient = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = tail,
                    EndPoint = head
                };
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(0, StarColor.R, StarColor.G, StarColor.B), 0));
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(220 * intensity), StarColor.R, StarColor.G, StarColor.B), 1));
                gradient.Freeze();

                var pen = new Pen(gradient, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                pen.Freeze();
                dc.DrawLine(pen, tail, head);
                dc.DrawEllipse(BrushFor(0.9 * intensity), null, head, 1.8, 1.8);
            }
        }

        private static SolidColorBrush BrushFor(double alpha)
        {
            int index = (int)Math.Round(Math.Clamp(alpha, 0, 1) * (AlphaLevels - 1));
            return StarBrushes[index];
        }

        private static SolidColorBrush[] CreateBrushes()
        {
            var brushes = new SolidColorBrush[AlphaLevels];
            for (int i = 0; i < AlphaLevels; i++)
            {
                byte a = (byte)(255.0 * i / (AlphaLevels - 1));
                var brush = new SolidColorBrush(Color.FromArgb(a, StarColor.R, StarColor.G, StarColor.B));
                brush.Freeze();
                brushes[i] = brush;
            }
            return brushes;
        }

        private struct Star
        {
            public double X, Y, Radius, Brightness, Drift, TwinkleSpeed, TwinklePhase, Life, Age;
        }

        private struct Meteor
        {
            public double X, Y, Vx, Vy, Length, Life, Age;
        }
    }
}
