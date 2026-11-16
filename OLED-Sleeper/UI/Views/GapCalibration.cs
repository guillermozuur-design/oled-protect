using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Native;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace OLED_Sleeper.UI.Views
{
    /// <summary>
    /// Calibrates the space between the screens. Every screen goes black and shows a few slanted lines that
    /// run across the bezel. Arrow keys, the mouse wheel or the slider change the gap until the lines look
    /// perfectly straight from one screen into the other. Enter saves, Esc cancels.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class GapCalibration
    {
        private readonly PanoramaSettings _settings;
        private readonly List<(Window Window, CalibrationCanvas Canvas)> _windows = new();
        private double _gapCm;
        private bool _closing;

        public GapCalibration(PanoramaSettings settings, IEnumerable<Rect> monitorBounds)
        {
            _settings = settings;
            _gapCm = settings.GapCm;

            var screens = monitorBounds.OrderBy(b => b.Left).ToList();

            for (int i = 0; i < screens.Count; i++)
            {
                var (index, count) = PanoramaLayout.Position(screens[i]);
                // Centimetres as a fraction of this screen's width: independent of Windows' display scaling.
                double fractionPerCm = ScreenPhysicalSize.PixelsPerCm(screens[i]) / screens[i].Width;
                var canvas = new CalibrationCanvas { Index = index, Count = count, FractionPerCm = fractionPerCm, GapCm = _gapCm };
                var window = BuildWindow(canvas, screens[i], showControls: i == 0);
                _windows.Add((window, canvas));
            }
        }

        public void Show()
        {
            foreach (var (window, _) in _windows) window.Show();
            _windows.FirstOrDefault().Window?.Activate();
        }

        private Window BuildWindow(CalibrationCanvas canvas, Rect bounds, bool showControls)
        {
            var root = new Grid { Background = Brushes.Black };
            root.Children.Add(canvas);

            var info = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 90) };
            var value = new TextBlock { FontSize = 34, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, FontFamily = new FontFamily("Bahnschrift") };
            canvas.ValueText = value;
            info.Children.Add(value);
            info.Children.Add(new TextBlock
            {
                Text = "Schuif tot de lijnen recht doorlopen over de rand tussen de schermen\n← / → of muiswiel = 0,1 cm   ·   Shift = 0,5 cm   ·   Enter = opslaan   ·   Esc = annuleren",
                FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 155)), TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 10, 0, 16), FontFamily = new FontFamily("Bahnschrift")
            });

            if (showControls)
            {
                var slider = new Slider { Minimum = 0, Maximum = 10, Width = 420, SmallChange = 0.1, LargeChange = 0.5, Value = _gapCm, IsSnapToTickEnabled = true, TickFrequency = 0.1 };
                slider.ValueChanged += (_, e) => SetGap(e.NewValue);
                canvas.Slider = slider;
                info.Children.Add(slider);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
                var save = new Button { Content = "Opslaan", Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 0, 10, 0) };
                save.Click += (_, _) => Finish(save: true);
                var cancel = new Button { Content = "Annuleren", Padding = new Thickness(18, 6, 18, 6) };
                cancel.Click += (_, _) => Finish(save: false);
                buttons.Children.Add(save);
                buttons.Children.Add(cancel);
                info.Children.Add(buttons);
            }
            root.Children.Add(info);

            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Background = Brushes.Black,
                Content = root,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Title = "Ruimte tussen schermen kalibreren"
            };

            window.SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, (int)bounds.Left, (int)bounds.Top,
                    (int)bounds.Width, (int)bounds.Height, 0);
            };
            window.PreviewKeyDown += OnKeyDown;
            window.PreviewMouseWheel += (_, e) => SetGap(_gapCm + Math.Sign(e.Delta) * 0.1);
            window.Closed += (_, _) => Finish(save: false);
            return window;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            double step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.5 : 0.1;
            switch (e.Key)
            {
                case Key.Left or Key.Down: SetGap(_gapCm - step); e.Handled = true; break;
                case Key.Right or Key.Up: SetGap(_gapCm + step); e.Handled = true; break;
                case Key.Enter: Finish(save: true); e.Handled = true; break;
                case Key.Escape: Finish(save: false); e.Handled = true; break;
            }
        }

        private void SetGap(double cm)
        {
            _gapCm = Math.Round(Math.Clamp(cm, 0, 10), 1);
            foreach (var (_, canvas) in _windows)
            {
                canvas.GapCm = _gapCm;
                if (canvas.Slider != null && Math.Abs(canvas.Slider.Value - _gapCm) > 0.001) canvas.Slider.Value = _gapCm;
            }
        }

        private void Finish(bool save)
        {
            if (_closing) return;
            _closing = true;
            if (save) _settings.GapCm = _gapCm;
            foreach (var (window, _) in _windows) window.Close();
        }

        /// <summary>Draws the slanted test lines for one screen, as part of one picture across all screens.</summary>
        private sealed class CalibrationCanvas : FrameworkElement
        {
            private double _gapCm;

            public int Index { get; init; }
            public int Count { get; init; }
            public double FractionPerCm { get; init; }
            public TextBlock? ValueText { get; set; }
            public Slider? Slider { get; set; }

            public double GapCm
            {
                get => _gapCm;
                set
                {
                    _gapCm = value;
                    if (ValueText != null) ValueText.Text = $"{value.ToString("0.0", CultureInfo.CurrentCulture)} cm";
                    InvalidateVisual();
                }
            }

            protected override void OnRender(DrawingContext dc)
            {
                double w = ActualWidth, h = ActualHeight;
                if (w <= 0) return;
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w, h));

                double gap = _gapCm * FractionPerCm * w;
                double offset = Index * (w + gap);                 // this screen's place in the big picture
                double seamX = w + gap / 2;                        // middle of the first bezel, in panorama pixels

                var pens = new[]
                {
                    new Pen(new SolidColorBrush(Color.FromRgb(90, 200, 255)), 3),
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2),
                    new Pen(new SolidColorBrush(Color.FromRgb(255, 190, 90)), 3),
                };

                // Three lines at different slants, all crossing the bezel near the middle of the screen height.
                double[] slopes = { 0.35, 0.8, -0.55 };
                double[] crossY = { h * 0.32, h * 0.5, h * 0.66 };
                for (int i = 0; i < slopes.Length; i++)
                {
                    double x0 = -offset, x1 = w * Count + gap * (Count - 1) - offset; // whole panorama in this screen's coordinates
                    double y0 = crossY[i] + (x0 + offset - seamX) * slopes[i];
                    double y1 = crossY[i] + (x1 + offset - seamX) * slopes[i];
                    dc.DrawLine(pens[i], new Point(x0, y0), new Point(x1, y1));
                }

                // A short horizontal reference at the bezel, to check heights match.
                var faint = new Pen(new SolidColorBrush(Color.FromRgb(70, 70, 75)), 1);
                dc.DrawLine(faint, new Point(-offset, h * 0.5), new Point(w * Count + gap * (Count - 1) - offset, h * 0.5));
            }
        }
    }
}
