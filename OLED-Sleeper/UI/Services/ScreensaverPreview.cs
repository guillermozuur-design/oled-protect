using OLED_Sleeper.Features.MonitorBlackout.Services;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using OLED_Sleeper.Native;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;

namespace OLED_Sleeper.UI.Services
{
    /// <summary>
    /// Shows the night-sky screensaver on the monitor under the mouse, using the real overlay window, until the
    /// mouse is moved. Shared by the tray menu, the quick panel and the "Sterrenhemel" tab.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ScreensaverPreview
    {
        private readonly IMonitorInfoManager _monitorInfoManager;
        private readonly PanoramaSettings _panorama;
        private OverlayWindow? _overlay;

        public ScreensaverPreview(IMonitorInfoManager monitorInfoManager, PanoramaSettings panorama)
        {
            _monitorInfoManager = monitorInfoManager;
            _panorama = panorama;
        }

        public void Show()
        {
            if (_overlay != null) return;

            NativeMethods.GetCursorPos(out var start);
            var monitors = _monitorInfoManager.GetLatestMonitorsBasicInfo();
            var monitor = monitors.FirstOrDefault(m => m.Bounds.Contains(new Point(start.X, start.Y))) ?? monitors.FirstOrDefault();
            if (monitor == null) return;

            _overlay = new OverlayWindow(screensaver: true, _panorama);
            _overlay.Show(monitor.Bounds);

            var startedAt = DateTime.UtcNow;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            poll.Tick += (_, _) =>
            {
                // Ignore the first second: the click that opened it still moves the mouse a bit.
                if ((DateTime.UtcNow - startedAt).TotalSeconds < 1)
                {
                    NativeMethods.GetCursorPos(out start);
                    return;
                }

                NativeMethods.GetCursorPos(out var now);
                if (Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) < 25) return;

                poll.Stop();
                _overlay?.Close();
                _overlay = null;
            };
            poll.Start();
        }
    }
}
