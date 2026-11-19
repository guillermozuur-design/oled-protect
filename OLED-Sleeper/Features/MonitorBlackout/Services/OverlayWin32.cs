using System.Runtime.InteropServices;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    // The few user32 calls the overlay needs to keep a WPF window out of the focus chain and on top.
    internal static partial class OverlayWin32
    {
        private const int ExtendedStyleIndex = -20;           // GWL_EXSTYLE
        private const long NoActivateStyle = 0x0800_0000;     // WS_EX_NOACTIVATE
        private const long ToolWindowStyle = 0x0000_0080;     // WS_EX_TOOLWINDOW (no Alt+Tab entry)

        private const uint KeepSize = 0x0001;                 // SWP_NOSIZE
        private const uint KeepPosition = 0x0002;             // SWP_NOMOVE
        private const uint KeepActivation = 0x0010;           // SWP_NOACTIVATE

        private static readonly nint TopmostBand = -1;        // HWND_TOPMOST

        // Clicks or focus must never land on the overlay, and it should not show up in Alt+Tab.
        public static void MakeUnfocusable(nint hwnd)
        {
            long style = ReadExtendedStyle(hwnd);
            WriteExtendedStyle(hwnd, style | NoActivateStyle | ToolWindowStyle);
        }

        // Places the window over a physical-pixel rectangle at the top of the topmost band.
        public static void CoverRectangle(nint hwnd, int left, int top, int width, int height) =>
            SetWindowPos(hwnd, TopmostBand, left, top, width, height, KeepActivation);

        // Other "always on top" windows (taskbar, popups) can climb over us; this pushes us back up.
        public static void RaiseToTop(nint hwnd) =>
            SetWindowPos(hwnd, TopmostBand, 0, 0, 0, 0, KeepActivation | KeepPosition | KeepSize);

        // The *Ptr variants only exist as exports in 64-bit user32.
        private static long ReadExtendedStyle(nint hwnd) =>
            Environment.Is64BitProcess ? GetWindowLongPtrW(hwnd, ExtendedStyleIndex) : GetWindowLongW(hwnd, ExtendedStyleIndex);

        private static void WriteExtendedStyle(nint hwnd, long style)
        {
            if (Environment.Is64BitProcess)
            {
                SetWindowLongPtrW(hwnd, ExtendedStyleIndex, (nint)style);
            }
            else
            {
                SetWindowLongW(hwnd, ExtendedStyleIndex, unchecked((int)style));
            }
        }

        [LibraryImport("user32.dll")]
        private static partial nint GetWindowLongPtrW(nint hwnd, int index);

        [LibraryImport("user32.dll")]
        private static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);

        [LibraryImport("user32.dll")]
        private static partial int GetWindowLongW(nint hwnd, int index);

        [LibraryImport("user32.dll")]
        private static partial int SetWindowLongW(nint hwnd, int index, int value);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    }
}
