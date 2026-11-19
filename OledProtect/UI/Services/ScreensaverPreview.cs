using OledProtect.Features.Flights;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.Sky;
using OledProtect.Features.WorkMode.Services;
using OledProtect.Native;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;

namespace OledProtect.UI.Services
{
    /// <summary>
    /// Shows the night-sky screensaver on the monitor under the mouse (or on all monitors), using the real overlay
    /// windows, until the mouse is moved or a key is pressed. Shared by the tray menu, the quick panel and the "Sterrenhemel" tab.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ScreensaverPreview
    {
        private readonly IMonitorInfoManager _monitorInfoManager;
        private readonly PanoramaSettings _panorama;
        private readonly FlightsSettings _flights;
        private readonly IMonitorIdleDetectionService? _idle;
        private readonly IWorkModeService? _workMode;
        private readonly List<OverlayWindow> _overlays = new();

        public ScreensaverPreview(IMonitorInfoManager monitorInfoManager, PanoramaSettings panorama, FlightsSettings flights,
                                  IMonitorIdleDetectionService? idle = null, IWorkModeService? workMode = null)
        {
            _monitorInfoManager = monitorInfoManager;
            _panorama = panorama;
            _flights = flights;
            _idle = idle;
            _workMode = workMode;
        }

        /// <param name="allScreens">True: the sky on every screen at once (one continuous panorama).
        /// False: only on the screen under the mouse.</param>
        /// <param name="scene">Stars or flights.</param>
        public void Show(bool allScreens = false, OverlayScene scene = OverlayScene.Stars)
        {
            if (_overlays.Count > 0) return;

            NativeMethods.GetCursorPos(out var start);
            var monitors = _monitorInfoManager.GetLatestMonitorsBasicInfo();
            var targets = allScreens
                ? monitors.ToList()
                : new[] { monitors.FirstOrDefault(m => m.Bounds.Contains(new Point(start.X, start.Y))) ?? monitors.FirstOrDefault() }
                    .Where(m => m != null).Select(m => m!).ToList();
            if (targets.Count == 0) return;

            // What you chose to watch wins: a screen that is already asleep (black, stars or planes of its own)
            // wakes first, and sleeping waits until you stop watching, so nothing is drawn half over it.
            _idle?.SetPaused(true);

            foreach (var monitor in targets)
            {
                var overlay = new OverlayWindow(scene, _panorama, _flights);
                overlay.Show(monitor.Bounds);
                _overlays.Add(overlay);
            }

            var startedAt = DateTime.UtcNow;
            bool keyPressed = false;
            var poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };

            // Any key ends the preview too, like a normal screensaver. A low-level keyboard hook sees every key,
            // whichever window has focus; it only exists while the preview is open.
            _keyHook = new LowLevelKeyboardHook(() =>
            {
                if ((DateTime.UtcNow - startedAt).TotalSeconds >= 1) keyPressed = true;
            });

            poll.Tick += (_, _) =>
            {
                // Ignore the first second: the click or keys that opened it are still settling.
                if ((DateTime.UtcNow - startedAt).TotalSeconds < 1)
                {
                    NativeMethods.GetCursorPos(out start);
                    return;
                }

                NativeMethods.GetCursorPos(out var now);
                bool mouseMoved = Math.Abs(now.X - start.X) + Math.Abs(now.Y - start.Y) >= 25;
                if (!mouseMoved && !keyPressed) return;

                poll.Stop();
                _keyHook?.Dispose();
                _keyHook = null;
                foreach (var overlay in _overlays) overlay.Close();
                _overlays.Clear();
                _idle?.SetPaused(_workMode?.IsEnabled ?? false);
            };
            poll.Start();
        }

        private LowLevelKeyboardHook? _keyHook;

        /// <summary>Calls back on every key press anywhere, until disposed.</summary>
        private sealed class LowLevelKeyboardHook : IDisposable
        {
            private const int WH_KEYBOARD_LL = 13;
            private const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
            private readonly HookProc _proc; // keep the delegate alive while hooked
            private readonly Action _onKey;
            private nint _hook;

            public LowLevelKeyboardHook(Action onKey)
            {
                _onKey = onKey;
                _proc = Callback;
                _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
            }

            private nint Callback(int code, nint wParam, nint lParam)
            {
                if (code >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)) _onKey();
                return CallNextHookEx(_hook, code, wParam, lParam);
            }

            public void Dispose()
            {
                if (_hook == nint.Zero) return;
                UnhookWindowsHookEx(_hook);
                _hook = nint.Zero;
            }

            private delegate nint HookProc(int code, nint wParam, nint lParam);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern nint SetWindowsHookEx(int id, HookProc proc, nint module, uint threadId);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern bool UnhookWindowsHookEx(nint hook);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

            [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            private static extern nint GetModuleHandle(string? name);
        }
    }
}
