using OledProtect.Features.Flights;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.Sky;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OledProtect.Features.MonitorBlackout.Services
{
    // Covers one monitor: fades it to black, and for a screensaver shows the SkyView night sky or
    // the FlightsView flight map.
    //
    // The fade is done by a FadeCurtain, a plain black Win32 window whose transparency Windows
    // blends on the GPU — smooth at any refresh rate (WPF's own transparency renders in software and stutters
    // on large, fast screens). Once the screen is black, the sky appears in a normal GPU-rendered WPF window on
    // top and its stars fade in. On wake-up the sky disappears and the black curtain fades out quickly.
    [ExcludeFromCodeCoverage]
    public class OverlayWindow : IOverlayWindow
    {
        /// <summary>Slow fade to black, so going idle is not a jarring flash.</summary>
        private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(1.5);

        /// <summary>Quick fade back, so waking the screen never feels laggy.</summary>
        private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(250);

        /// <summary>How long the stars take to appear once the screen is black.</summary>
        private static readonly TimeSpan StarsFadeIn = TimeSpan.FromSeconds(1.2);

        // Topmost windows that appear later (taskbar, notifications) can end up above us; we climb back
        // over them on this beat for as long as the overlay is up.
        private static readonly TimeSpan StayOnTopBeat = TimeSpan.FromMilliseconds(1000);

        private readonly OverlayScene _scene;
        private readonly PanoramaSettings? _panorama;
        private readonly FlightsSettings? _flights;
        private readonly DispatcherTimer _stayOnTop;
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

            // A running DispatcherTimer is rooted by the dispatcher, so Close must stop it or this
            // overlay would never be collected.
            _stayOnTop = new DispatcherTimer(DispatcherPriority.Normal) { Interval = StayOnTopBeat };
            _stayOnTop.Tick += OnStayOnTopBeat;
        }

        /// <summary>The black curtain's handle (what the rest of the app knows this overlay by).</summary>
        public nint Handle => _curtain?.Handle ?? nint.Zero;

        public void Show(Rect bounds)
        {
            _bounds = bounds;
            _curtain = new FadeCurtain(bounds);
            _stayOnTop.Start();

            // Flights: start fetching planes and the map now, during the fade to black, so they are there as
            // soon as the screen is black (instead of only starting then).
            if (_scene == OverlayScene.Flights && _flights != null)
            {
                _flightsWarmUp = FlightTour.WarmUp(_flights);
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
            _stayOnTop.Tick -= OnStayOnTopBeat;
            _stayOnTop.Stop();
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

            _skyWindow = CreateSkyHost(_sky);
            _skyWindow.Show();

            // WPF positions in device-independent units; the bounds are physical pixels, so place the
            // window through Win32 once it has a handle.
            nint hwnd = SkyHandle();
            if (hwnd != nint.Zero)
            {
                OverlayWin32.CoverRectangle(hwnd, (int)_bounds.Left, (int)_bounds.Top, (int)_bounds.Width, (int)_bounds.Height);
            }

            // GPU-rendered content fade: the stars come up softly out of the black.
            _sky.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, StarsFadeIn) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        // An opaque, frameless, cursorless host that starts far off-screen so nothing flashes on the
        // wrong monitor before it is moved into place.
        private static Window CreateSkyHost(FrameworkElement content)
        {
            var host = new Window { Content = content, Left = -32000, Top = -32000 };
            host.WindowStartupLocation = WindowStartupLocation.Manual;
            host.WindowStyle = WindowStyle.None;
            host.ResizeMode = ResizeMode.NoResize;
            host.AllowsTransparency = false; // opaque keeps WPF on its GPU path
            host.Background = Brushes.Black;
            host.Cursor = Cursors.None;
            host.Topmost = true;
            host.ShowActivated = false;
            host.ShowInTaskbar = false;

            // Styles are set as soon as the handle exists, before the window is ever visible.
            host.SourceInitialized += (_, _) => OverlayWin32.MakeUnfocusable(new WindowInteropHelper(host).Handle);
            return host;
        }

        private nint SkyHandle() => _skyWindow == null ? nint.Zero : new WindowInteropHelper(_skyWindow).Handle;

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

        // The curtain goes up first so the sky, raised second, stays above it.
        private void OnStayOnTopBeat(object? sender, EventArgs e)
        {
            _curtain?.BringToTop();

            nint sky = SkyHandle();
            if (sky != nint.Zero)
            {
                OverlayWin32.RaiseToTop(sky);
            }
        }
    }
}
