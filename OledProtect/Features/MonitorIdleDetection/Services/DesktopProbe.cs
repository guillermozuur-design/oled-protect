using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;

namespace OledProtect.Features.MonitorIdleDetection.Services
{
    // Everything idle detection needs to know about the desktop at one instant.
    internal readonly record struct DesktopSnapshot(
        uint MillisecondsSinceInput,
        Point Cursor,
        nint FocusedWindow,
        Rect FocusedWindowArea,
        bool DisplayRequired);

    // Reads the desktop state from Win32. Kept apart from the decision logic so that logic stays testable.
    [ExcludeFromCodeCoverage]
    internal static partial class DesktopProbe
    {
        public static DesktopSnapshot Take()
        {
            nint focused = GetForegroundWindow();
            return new DesktopSnapshot(
                MillisecondsSinceInput(),
                CursorPosition(),
                focused,
                AreaOf(focused),
                SomeoneNeedsTheDisplay());
        }

        // GetLastInputInfo reports a tick count; unsigned subtraction keeps working across the 49-day wrap.
        private static uint MillisecondsSinceInput()
        {
            var info = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
            return GetLastInputInfo(ref info) ? unchecked((uint)Environment.TickCount - info.Tick) : 0;
        }

        private static Point CursorPosition()
        {
            GetCursorPos(out var p);
            return new Point(p.X, p.Y);
        }

        // The visible frame of the focused window. The desktop itself (Progman / WorkerW) spans every
        // screen, so it is reported as no area at all rather than as "focus on every screen".
        private static Rect AreaOf(nint window)
        {
            if (window == nint.Zero || IsShellDesktop(window))
            {
                return Rect.Empty;
            }

            // DWM's frame bounds leave out the invisible resize border that GetWindowRect includes.
            const int ExtendedFrameBounds = 9;
            if (DwmGetWindowAttribute(window, ExtendedFrameBounds, out var frame, Marshal.SizeOf<Edges>()) != 0)
            {
                GetWindowRect(window, out frame);
            }

            return new Rect(frame.Left, frame.Top, Math.Max(0, frame.Right - frame.Left), Math.Max(0, frame.Bottom - frame.Top));
        }

        private static unsafe bool IsShellDesktop(nint window)
        {
            const int Capacity = 64;
            char* buffer = stackalloc char[Capacity];
            int length = GetClassNameW(window, buffer, Capacity);
            if (length <= 0)
            {
                return false;
            }

            var className = new ReadOnlySpan<char>(buffer, length);
            return className.SequenceEqual("Progman") || className.SequenceEqual("WorkerW");
        }

        // True while a program holds ES_DISPLAY_REQUIRED, which is what video players and browsers do
        // during playback. Read from the combined execution state of the whole system.
        private static bool SomeoneNeedsTheDisplay()
        {
            const int SystemExecutionStateLevel = 16;
            const uint DisplayRequiredFlag = 0x2;
            try
            {
                return CallNtPowerInformation(SystemExecutionStateLevel, nint.Zero, 0, out uint flags, sizeof(uint)) == 0
                       && (flags & DisplayRequiredFlag) != 0;
            }
            catch (Exception)
            {
                // Missing power API (stripped-down Windows): simply behave as if no video is playing.
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInput
        {
            public uint Size;
            public uint Tick;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenPoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Edges
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetLastInputInfo(ref LastInput info);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out ScreenPoint point);

        [LibraryImport("user32.dll")]
        private static partial nint GetForegroundWindow();

        [LibraryImport("user32.dll")]
        private static unsafe partial int GetClassNameW(nint window, char* buffer, int capacity);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetWindowRect(nint window, out Edges area);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmGetWindowAttribute(nint window, int attribute, out Edges value, int size);

        [LibraryImport("powrprof.dll")]
        private static partial uint CallNtPowerInformation(int level, nint input, uint inputSize, out uint output, uint outputSize);
    }
}
