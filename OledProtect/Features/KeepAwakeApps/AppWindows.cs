using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace OledProtect.Features.KeepAwakeApps
{
    /// <summary>A visible top-level window of some app.</summary>
    public readonly record struct AppWindow(string ProcessName, string Title, Rect Bounds, bool Minimized);

    /// <summary>
    /// Decides, from the open windows and the chosen apps, which screens must stay awake. Pure logic, so it
    /// can be tested without real windows.
    /// </summary>
    public static class KeepAwakeEvaluator
    {
        /// <param name="windows">The open windows.</param>
        /// <param name="rules">The apps that are switched on.</param>
        /// <returns>
        /// <c>AllScreens</c>: an "all screens" app is open (even minimised). <c>ScreenAreas</c>: the rectangles of
        /// "this screen" apps that are actually shown (not minimised); a screen they overlap stays awake.
        /// </returns>
        public static (bool AllScreens, IReadOnlyList<Rect> ScreenAreas) Evaluate(
            IEnumerable<AppWindow> windows, IReadOnlyList<KeepAwakeRule> rules)
        {
            bool all = false;
            var areas = new List<Rect>();
            if (rules.Count == 0) return (false, areas);

            foreach (var window in windows)
            {
                foreach (var rule in rules)
                {
                    if (!rule.Matches(window.ProcessName)) continue;
                    if (rule.Scope == KeepAwakeScope.AllScreens) all = true;
                    else if (!window.Minimized) areas.Add(window.Bounds);
                }
            }
            return (all, areas);
        }

        /// <summary>Whether a "this screen" app shows a meaningful part (at least 1/5 of the window) on these bounds.</summary>
        public static bool KeepsScreenAwake(Rect screen, IReadOnlyList<Rect> areas)
        {
            foreach (var area in areas)
            {
                var overlap = Rect.Intersect(screen, area);
                if (overlap.IsEmpty) continue;
                double windowSize = Math.Max(1, area.Width * area.Height);
                if (overlap.Width * overlap.Height >= windowSize / 5) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Lists the visible top-level windows with the name of the program they belong to. Leaves out invisible,
    /// cloaked (hidden UWP), untitled and tool windows, and this app's own windows.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class AppWindowScanner
    {
        private static readonly Dictionary<uint, string> ProcessNameCache = new();
        private static readonly int OwnProcessId = Environment.ProcessId;

        public static List<AppWindow> Scan()
        {
            var result = new List<AppWindow>();
            var seenPids = new HashSet<uint>();

            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
                long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;

                int length = GetWindowTextLength(hwnd);
                if (length == 0) return true;
                var title = new StringBuilder(length + 1);
                GetWindowText(hwnd, title, title.Capacity);

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == OwnProcessId) return true;
                seenPids.Add(pid);

                var name = ProcessName(pid);
                if (string.IsNullOrEmpty(name)) return true;

                // Store apps (Films & TV, Netflix, …) show their window through ApplicationFrameHost; the real
                // app is the process behind one of its child windows.
                if (string.Equals(name, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                {
                    uint hostPid = pid;
                    string? inner = null;
                    EnumChildWindows(hwnd, (child, _) =>
                    {
                        GetWindowThreadProcessId(child, out uint childPid);
                        if (childPid == hostPid) return true;
                        inner = ProcessName(childPid);
                        if (!string.IsNullOrEmpty(inner)) seenPids.Add(childPid);
                        return string.IsNullOrEmpty(inner);
                    }, nint.Zero);
                    if (!string.IsNullOrEmpty(inner)) name = inner;
                }

                GetWindowRect(hwnd, out var r);
                bool minimized = IsIconic(hwnd);
                result.Add(new AppWindow(name, title.ToString(), new Rect(r.Left, r.Top, Math.Max(0, r.Right - r.Left), Math.Max(0, r.Bottom - r.Top)), minimized));
                return true;
            }, nint.Zero);

            // Forget processes that are gone, so a reused id cannot get an old name.
            lock (ProcessNameCache)
            {
                foreach (var pid in ProcessNameCache.Keys.Where(p => !seenPids.Contains(p)).ToList()) ProcessNameCache.Remove(pid);
            }
            return result;
        }

        private static string ProcessName(uint pid)
        {
            lock (ProcessNameCache)
            {
                if (ProcessNameCache.TryGetValue(pid, out var cached)) return cached;
                string name;
                try
                {
                    using var process = Process.GetProcessById((int)pid);
                    name = process.ProcessName;
                }
                catch
                {
                    name = string.Empty;
                }
                ProcessNameCache[pid] = name;
                return name;
            }
        }

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080;
        private const int DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(nint hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int max);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out RECT rect);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    }
}
