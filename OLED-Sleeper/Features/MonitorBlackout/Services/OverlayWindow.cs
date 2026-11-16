using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Native;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    /// <summary>
    /// A borderless, topmost, non-activating window that covers one monitor. It fades in slowly and
    /// fades out quickly, and shows either plain black or the <see cref="SkyView"/> night-sky screensaver.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OverlayWindow : IOverlayWindow
    {
        private static readonly TimeSpan TopmostReassertInterval = TimeSpan.FromSeconds(1);

        /// <summary>Slow fade to black, so going idle is not a jarring flash.</summary>
        private static readonly TimeSpan FadeInDuration = TimeSpan.FromSeconds(1.5);

        /// <summary>Quick fade back, so waking the screen never feels laggy.</summary>
        private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(250);

        private readonly Window _window = new()
        {
            Cursor = System.Windows.Input.Cursors.None,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Black,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Opacity = 0
        };

        private readonly DispatcherTimer _topmostTimer;
        private readonly SkyView? _starfield;
        private bool _closing;

        public OverlayWindow(bool screensaver = false)
        {
            _topmostTimer = new DispatcherTimer { Interval = TopmostReassertInterval };
            _topmostTimer.Tick += (_, _) => ReassertTopmost();

            if (screensaver)
            {
                _starfield = new SkyView();
                _window.Content = _starfield;
            }
        }

        public nint Handle => new WindowInteropHelper(_window).Handle;

        public void Show(Rect bounds)
        {
            _window.Show();

            nint hwnd = Handle;
            if (hwnd == nint.Zero) return;

            ApplyNoActivateStyle(hwnd);
            if (_starfield != null && bounds.Width > 0)
            {
                // Screens next to each other show neighbouring parts of the same sky.
                _starfield.PanoramaIndex = Math.Round(bounds.Left / bounds.Width);
            }

            PositionToMonitor(hwnd, bounds);
            _topmostTimer.Start();

            _window.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, FadeInDuration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;

            // A running DispatcherTimer is held by the dispatcher, which keeps this overlay alive.
            _topmostTimer.Stop();

            var fadeOut = new DoubleAnimation(_window.Opacity, 0, FadeOutDuration);
            fadeOut.Completed += (_, _) =>
            {
                _starfield?.Stop();
                _window.Close();
            };
            _window.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        #region Private Helpers

        private static void PositionToMonitor(nint hwnd, Rect bounds)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                (int)bounds.Left,
                (int)bounds.Top,
                (int)bounds.Width,
                (int)bounds.Height,
                NativeMethods.SWP_NOACTIVATE);
        }

        private static void ApplyNoActivateStyle(nint hwnd)
        {
            nint extendedStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE,
                new nint(extendedStyle.ToInt64() | NativeMethods.WS_EX_NOACTIVATE));
        }

        private void ReassertTopmost()
        {
            nint hwnd = Handle;
            if (hwnd == nint.Zero) return;

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE);
        }

        #endregion Private Helpers
    }
}
