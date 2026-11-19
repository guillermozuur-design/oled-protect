using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;

namespace OledProtect.Features.MonitorBlackout.Services
{
    /// <summary>
    /// A plain black Win32 window (no WPF) whose whole-window transparency is set by Windows itself
    /// (<c>WS_EX_LAYERED</c> + <c>SetLayeredWindowAttributes</c>). The desktop compositor blends it on the GPU,
    /// so fading it is smooth at any refresh rate and costs next to nothing. WPF windows cannot do this:
    /// their GPU output ignores the layered alpha, and WPF's own transparency renders in software.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class FadeCurtain : IDisposable
    {
        private const string ClassName = "OLEDSpaceSleeperCurtain";
        private static WndProcDelegate? _wndProc; // kept alive for the lifetime of the class registration
        private static bool _registered;

        public nint Handle { get; private set; }

        public FadeCurtain(Rect bounds)
        {
            EnsureClass();
            Handle = CreateWindowEx(
                WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
                ClassName, string.Empty, WS_POPUP,
                (int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height,
                nint.Zero, nint.Zero, GetModuleHandle(null), nint.Zero);
            Serilog.Log.Debug("FadeCurtain created: handle {Handle}, error {Error}, bounds {Bounds}", Handle, Marshal.GetLastWin32Error(), bounds);
            if (Handle == nint.Zero) return;

            SetAlpha(0);
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>0 = invisible, 1 = solid black.</summary>
        public void SetAlpha(double alpha)
        {
            if (Handle == nint.Zero) return;
            SetLayeredWindowAttributes(Handle, 0, (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255), LWA_ALPHA);
        }

        public void BringToTop()
        {
            if (Handle != nint.Zero)
                SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void Dispose()
        {
            if (Handle == nint.Zero) return;
            DestroyWindow(Handle);
            Handle = nint.Zero;
        }

        private static void EnsureClass()
        {
            if (_registered) return;
            _wndProc = WndProc;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = GetModuleHandle(null),
                hbrBackground = GetStockObject(BLACK_BRUSH),
                lpszClassName = ClassName
            };
            var atom = RegisterClassEx(ref wc);
            Serilog.Log.Debug("FadeCurtain class registered: atom {Atom}, error {Error}", atom, Marshal.GetLastWin32Error());
            _registered = true;
        }

        private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
        {
            switch (msg)
            {
                case WM_SETCURSOR:
                    SetCursor(nint.Zero); // no mouse pointer over the black screen
                    return 1;
                case WM_MOUSEACTIVATE:
                    return MA_NOACTIVATE;
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        // ---- Win32 ----
        private delegate nint WndProcDelegate(nint hwnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize, style;
            public nint lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public nint hInstance, hIcon, hCursor, hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public nint hIconSm;
        }

        private const uint WS_POPUP = 0x80000000;
        private const uint WS_EX_LAYERED = 0x00080000, WS_EX_TOPMOST = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        private const uint LWA_ALPHA = 0x2;
        private const int SW_SHOWNOACTIVATE = 4;
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
        private static readonly nint HWND_TOPMOST = new(-1);
        private const int BLACK_BRUSH = 4;
        private const uint WM_SETCURSOR = 0x0020, WM_MOUSEACTIVATE = 0x0021;
        private const nint MA_NOACTIVATE = 3;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h,
                                                  nint parent, nint menu, nint instance, nint param);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int cmd);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] private static extern nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);
        [DllImport("user32.dll")] private static extern nint SetCursor(nint cursor);
        [DllImport("gdi32.dll")] private static extern nint GetStockObject(int obj);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    }
}
