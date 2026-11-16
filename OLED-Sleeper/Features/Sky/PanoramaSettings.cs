using OLED_Sleeper.Storage.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;

namespace OLED_Sleeper.Features.Sky
{
    /// <summary>
    /// Settings for the sky panorama across several screens: how much physical space (the bezels) sits
    /// between two screens. That space is treated as an invisible strip of sky, so a line or a shooting star
    /// that disappears behind the bezel comes out on the other side exactly where you would expect, like
    /// looking through a window with a frame. Remembered in <c>panorama.json</c>.
    /// </summary>
    public class PanoramaSettings
    {
        private const string FileName = "panorama.json";
        public const double DefaultGapCm = 2.0;

        private sealed class Stored
        {
            public double GapCm { get; set; } = DefaultGapCm;
        }

        private readonly IAppDataFileStore _fileStore;
        private double _gapCm;

        public PanoramaSettings(IAppDataFileStore fileStore)
        {
            _fileStore = fileStore;
            _gapCm = Math.Clamp(_fileStore.Read<Stored>(FileName)?.GapCm ?? DefaultGapCm, 0, 30);
        }

        /// <summary>Fires when the gap changed.</summary>
        public event Action? Changed;

        /// <summary>Space between two neighbouring screens, in centimetres (0-30).</summary>
        public double GapCm
        {
            get => _gapCm;
            set
            {
                var clamped = Math.Clamp(value, 0, 30);
                if (Math.Abs(clamped - _gapCm) < 0.001) return;
                _gapCm = clamped;
                _fileStore.TryWrite(FileName, new Stored { GapCm = clamped });
                Changed?.Invoke();
            }
        }

        /// <summary>The gap in pixels for a screen with these bounds (physical pixels).</summary>
        public double GapPixels(Rect bounds) => GapCm * ScreenPhysicalSize.PixelsPerCm(bounds);
    }

    /// <summary>
    /// How many pixels make one centimetre on a screen, from the physical size the monitor reports (EDID).
    /// Falls back to a 27-inch 16:9 panel when the monitor reports nothing useful.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ScreenPhysicalSize
    {
        private const int HORZSIZE = 4; // width in millimetres
        private const double FallbackWidthCm = 59.8;

        public static double PixelsPerCm(Rect bounds)
        {
            if (bounds.Width <= 0) return bounds.Width / FallbackWidthCm;
            try
            {
                var center = new POINT { X = (int)(bounds.Left + bounds.Width / 2), Y = (int)(bounds.Top + bounds.Height / 2) };
                var monitor = MonitorFromPoint(center, 2 /* MONITOR_DEFAULTTONEAREST */);
                var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
                if (monitor != nint.Zero && GetMonitorInfo(monitor, ref info))
                {
                    var hdc = CreateDC(null, info.szDevice, null, nint.Zero);
                    if (hdc != nint.Zero)
                    {
                        int mm = GetDeviceCaps(hdc, HORZSIZE);
                        DeleteDC(hdc);
                        if (mm > 100) return bounds.Width / (mm / 10.0);
                    }
                }
            }
            catch
            {
                // fall through to the default
            }
            return bounds.Width / FallbackWidthCm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor, rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] private static extern nint MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateDC(string? driver, string device, string? output, nint initData);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(nint hdc, int index);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint hdc);
    }
}
