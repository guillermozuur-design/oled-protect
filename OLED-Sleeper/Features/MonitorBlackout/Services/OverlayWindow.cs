using OLED_Sleeper.Features.Flights;
using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Native;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    /// <summary>
    /// Covers one monitor: fades it to black, and for a screensaver shows the <see cref="SkyView"/> night sky or
    /// the <see cref="FlightsView"/> flight map.
    ///
    /// The fade is done by a <see cref="FadeCurtain"/>, a plain black Win32 window whose transparency Windows
    /// blends on the GPU — smooth at any refresh rate (WPF's own transparency renders in software and stutters
    /// on large, fast screens). Once the screen is black, the sky appears in a normal GPU-rendered WPF window on
    /// top and its stars fade in. On wake-up the sky disappears and the black curtain fades out quickly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OverlayWindow : IOverlayWindow
    {
        private static readonly TimeSpan TopmostReassertInterval = TimeSpan.FromSeconds(1);

        /// <summary>Slow fade to black, so going idle is not a jarring flash.</summary>
        private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(1.5);

        /// <summary>Quick fade back, so waking the screen never feels laggy.</summary>
        private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(250);

        /// <summary>How long the stars take to appear once the screen is black.</summary>
        private static readonly TimeSpan StarsFadeIn = TimeSpan.FromSeconds(1.2);

        private readonly OverlayScene _scene;
        private readonly PanoramaSettings? _panorama;
        private readonly FlightsSettings? _flights;
        private readonly DispatcherTimer _topmostTimer;
        private FadeCurtain? _curtain;
        private Window? _skyWindow;
        private FrameworkElement? _sky;
        private Rect _bounds;
        private bool _closing;
        private IDisposable? _flightsWarmUp;

        // Fade state, driven once per display frame.
        private readonly Stopwatch _fadeClock = new();
        private double _fadeFrom, _fadeTo, _alpha;
        private TimeSpan _fadeDuration;
        private Action? _fadeDone;
        private bool _fading;

        public OverlayWindow(OverlayScene scene = OverlayScene.Black, PanoramaSettings? panorama = null, FlightsSettings? flights = null)
        {
            _scene = scene == OverlayScene.Flights && flights == null ? OverlayScene.Stars : scene;
            _panorama = panorama;
            _flights = flights;
            _topmostTimer = new DispatcherTimer { Interval = TopmostReassertInterval };
            _topmostTimer.Tick += (_, _) => ReassertTopmost();
        }

        /// <summary>The black curtain's handle (what the rest of the app knows this overlay by).</summary>
        public nint Handle => _curtain?.Handle ?? nint.Zero;

        public void Show(Rect bounds)
        {
            _bounds = bounds;
            _curtain = new FadeCurtain(bounds);
            _topmostTimer.Start();

            // Flights: start fetching planes and the map now, during the fade to black, so they are there as
            // soon as the screen is black (instead of only starting then).
            if (_scene == OverlayScene.Flights && _flights != null)
            {
                _flightsWarmUp = AircraftFeed.Subscribe(_flights.Latitude, _flights.Longitude, FlightsView.FeedRadiusKm(_flights));
                _ = MapLoader.GetAsync(_flights.Latitude, _flights.Longitude, _flights.RadiusKm, _flights.CacheDirectory);
            }
            StartFade(0, 1, FadeInDuration, () =>
            {
                if (_scene != OverlayScene.Black && !_closing) ShowSky();
            });
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            _topmostTimer.Stop();
            _flightsWarmUp?.Dispose();
            _flightsWarmUp = null;

            // Stars go at once (the curtain underneath is black), then the black fades away.
            CloseSky();
            StartFade(_alpha, 0, FadeOutDuration, () =>
            {
                _curtain?.Dispose();
                _curtain = null;
            });
        }

        // ================= the sky on top of the black =================

        private void ShowSky()
        {
            // Screens next to each other show neighbouring parts of the same sky (or map).
            var (index, count) = _bounds.Width > 0 ? PanoramaLayout.Position(_bounds) : (0, 1);
            double gap = _bounds.Width > 0 ? _panorama?.GapFraction(_bounds) ?? 0 : 0;
            if (_scene == OverlayScene.Flights)
                _sky = new FlightsView(_flights!) { Opacity = 0, PanoramaIndex = index, PanoramaCount = count, GapFraction = gap };
            else
                _sky = new SkyView { Opacity = 0, PanoramaIndex = index, PanoramaCount = count, GapFraction = gap };

            _skyWindow = new Window
            {
                Cursor = System.Windows.Input.Cursors.None,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = false,
                Background = Brushes.Black,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -32000,
                Top = -32000,
                Content = _sky
            };
            _skyWindow.Show();

            nint hwnd = new WindowInteropHelper(_skyWindow).Handle;
            if (hwnd != nint.Zero)
            {
                nint style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
                NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
                    new nint(style.ToInt64() | NativeMethods.WS_EX_NOACTIVATE | 0x80 /* WS_EX_TOOLWINDOW */));
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST,
                    (int)_bounds.Left, (int)_bounds.Top, (int)_bounds.Width, (int)_bounds.Height, NativeMethods.SWP_NOACTIVATE);
            }

            // GPU-rendered content fade: the stars come up softly out of the black.
            _sky.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, StarsFadeIn) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        private void CloseSky()
        {
            (_sky as SkyView)?.Stop();
            (_sky as FlightsView)?.Stop();
            _skyWindow?.Close();
            _skyWindow = null;
            _sky = null;
        }

        // ================= fade of the black curtain =================

        private void StartFade(double from, double to, TimeSpan duration, Action? done)
        {
            _fadeFrom = from;
            _fadeTo = to;
            _fadeDuration = duration;
            _fadeDone = done;
            _fadeClock.Restart();
            if (!_fading)
            {
                _fading = true;
                CompositionTarget.Rendering += OnFadeFrame;
            }
        }

        /// <summary>Called once per display frame while fading: smooth at any refresh rate.</summary>
        private void OnFadeFrame(object? sender, EventArgs e)
        {
            double t = _fadeDuration.TotalMilliseconds <= 0 ? 1 : Math.Min(1, _fadeClock.Elapsed.TotalMilliseconds / _fadeDuration.TotalMilliseconds);
            double eased = 0.5 - 0.5 * Math.Cos(Math.PI * t); // ease in-out
            _alpha = _fadeFrom + (_fadeTo - _fadeFrom) * eased;
            _curtain?.SetAlpha(_alpha);

            if (t >= 1)
            {
                CompositionTarget.Rendering -= OnFadeFrame;
                _fading = false;
                var done = _fadeDone;
                _fadeDone = null;
                done?.Invoke();
            }
        }

        private void ReassertTopmost()
        {
            // Keep the curtain (and the sky above it) on top of windows that also claim "topmost".
            _curtain?.BringToTop();
            if (_skyWindow == null) return;
            nint hwnd = new WindowInteropHelper(_skyWindow).Handle;
            if (hwnd != nint.Zero)
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
        }
    }
}
