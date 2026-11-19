using OledProtect.Storage.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OledProtect.Features.TaskbarDim
{
    /// <summary>The "Taakbalk dimmen" switch in the Algemeen tab, remembered in <c>taskbar.json</c>.</summary>
    public class TaskbarDimSettings
    {
        private const string FileName = "taskbar.json";

        public sealed class Stored
        {
            public bool Enabled { get; set; }
        }

        private readonly IAppDataFileStore? _fileStore;
        private bool _enabled;

        public TaskbarDimSettings(IAppDataFileStore? fileStore = null)
        {
            _fileStore = fileStore;
            _enabled = _fileStore?.Read<Stored>(FileName)?.Enabled ?? false;
        }

        public event Action? Changed;

        public bool Enabled => _enabled;

        public void SetEnabled(bool enabled)
        {
            if (enabled == _enabled) return;
            _enabled = enabled;
            _fileStore?.TryWrite(FileName, new Stored { Enabled = enabled });
            Changed?.Invoke();
        }
    }

    /// <summary>When the taskbar is dimmed (no window or clock here, so it can be tested).</summary>
    public static class TaskbarDimRules
    {
        /// <summary>This long without the mouse near the taskbar, and it goes darker.</summary>
        public static readonly TimeSpan After = TimeSpan.FromSeconds(5);

        /// <summary>How dark: 0 = not at all, 1 = black.</summary>
        public const double Strength = 0.55;

        /// <summary>The mouse counts as "at the taskbar" this close to it (pixels), so it brightens just before.</summary>
        public const int Reach = 24;

        public static bool IsNear(Rect taskbar, Point cursor)
        {
            var near = taskbar;
            near.Inflate(Reach, Reach);
            return near.Contains(cursor);
        }

        /// <summary>A taskbar that is hidden (auto-hide) is only a sliver on screen: nothing to dim.</summary>
        public static bool IsShown(Rect taskbar, Rect monitor)
        {
            var visible = Rect.Intersect(taskbar, monitor);
            return !visible.IsEmpty && Math.Min(visible.Width, visible.Height) >= 16;
        }

        public static bool ShouldDim(DateTime lastNearUtc, DateTime nowUtc) => nowUtc - lastNearUtc >= After;
    }

    /// <summary>
    /// Lays a dark, see-through strip over each taskbar after <see cref="TaskbarDimRules.After"/> without the
    /// mouse near it, so its icons and clock wear an OLED screen less. Clicks go straight through; when the
    /// mouse comes near it is gone at once. Nothing happens while a program is full screen.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class TaskbarDimmer
    {
        private readonly TaskbarDimSettings _settings;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly Dictionary<nint, (Window Shade, DateTime LastNear, bool Dimmed)> _shades = new();

        public TaskbarDimmer(TaskbarDimSettings settings)
        {
            _settings = settings;
            _timer.Tick += (_, _) => Tick();
        }

        public void Start()
        {
            _settings.Changed += Apply;
            Apply();
        }

        private void Apply()
        {
            if (_settings.Enabled) _timer.Start();
            else
            {
                _timer.Stop();
                foreach (var s in _shades.Values) s.Shade.Close();
                _shades.Clear();
            }
        }

        private void Tick()
        {
            var now = DateTime.UtcNow;
            GetCursorPos(out var c);
            var cursor = new Point(c.X, c.Y);
            var taskbars = FindTaskbars();
            bool fullScreen = IsFullScreenInFront();

            foreach (var gone in _shades.Keys.Where(h => !taskbars.ContainsKey(h)).ToList())
            {
                _shades[gone].Shade.Close();
                _shades.Remove(gone);
            }

            foreach (var (handle, (rect, monitor)) in taskbars)
            {
                if (!_shades.TryGetValue(handle, out var entry))
                    entry = (CreateShade(), now, false);

                bool near = TaskbarDimRules.IsNear(rect, cursor);
                if (near) entry.LastNear = now;
                bool dim = !near && !fullScreen && TaskbarDimRules.IsShown(rect, monitor) && TaskbarDimRules.ShouldDim(entry.LastNear, now);

                if (dim && !entry.Dimmed) Show(entry.Shade, rect);
                else if (!dim && entry.Dimmed) Hide(entry.Shade);
                else if (dim) Place(entry.Shade, rect, topmost: false); // follow the taskbar if it moved
                entry.Dimmed = dim;
                _shades[handle] = entry;
            }
        }

        private static Window CreateShade()
        {
            var shade = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb((byte)(TaskbarDimRules.Strength * 255), 0, 0, 0)),
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                ResizeMode = ResizeMode.NoResize,
                Opacity = 0,
                Width = 1,
                Height = 1,
                Left = -32000,
                Top = -32000,
            };
            shade.SourceInitialized += (_, _) =>
            {
                // Click-through, never takes focus, not in Alt+Tab.
                var hwnd = new WindowInteropHelper(shade).Handle;
                long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
            };
            shade.Show();
            return shade;
        }

        private static void Show(Window shade, Rect rect)
        {
            Place(shade, rect, topmost: true);
            shade.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(700)));
        }

        private static void Hide(Window shade) =>
            shade.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));

        /// <summary>Exactly over the taskbar, in screen pixels (above it, since the taskbar is topmost too).</summary>
        private static void Place(Window shade, Rect rect, bool topmost)
        {
            var hwnd = new WindowInteropHelper(shade).Handle;
            if (hwnd == nint.Zero) return;
            SetWindowPos(hwnd, topmost ? HWND_TOPMOST : nint.Zero, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                         SWP_NOACTIVATE | (topmost ? 0 : SWP_NOZORDER));
        }

        /// <summary>Every taskbar (main and on other screens) with its rectangle and its screen.</summary>
        private static Dictionary<nint, (Rect Rect, Rect Monitor)> FindTaskbars()
        {
            var found = new Dictionary<nint, (Rect, Rect)>();
            foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
            {
                nint h = nint.Zero;
                while ((h = FindWindowEx(nint.Zero, h, cls, null)) != nint.Zero)
                {
                    if (!IsWindowVisible(h) || !GetWindowRect(h, out var r)) continue;
                    var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (!GetMonitorInfo(MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST), ref info)) continue;
                    found[h] = (ToRect(r), ToRect(info.rcMonitor));
                }
            }
            return found;
        }

        /// <summary>A program in front covers its whole screen (a game or video full screen).</summary>
        private static bool IsFullScreenInFront()
        {
            var fg = GetForegroundWindow();
            if (fg == nint.Zero || !GetWindowRect(fg, out var r)) return false;
            var cls = new System.Text.StringBuilder(64);
            GetClassName(fg, cls, cls.Capacity);
            if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false; // the desktop itself
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST), ref info)) return false;
            return r.Left <= info.rcMonitor.Left && r.Top <= info.rcMonitor.Top && r.Right >= info.rcMonitor.Right && r.Bottom >= info.rcMonitor.Bottom;
        }

        private static Rect ToRect(RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        // ---- Win32 ----
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
        private static readonly nint HWND_TOPMOST = new(-1);
        private const uint SWP_NOACTIVATE = 0x0010, SWP_NOZORDER = 0x0004;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string cls, string? title);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, System.Text.StringBuilder name, int max);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    }
}
