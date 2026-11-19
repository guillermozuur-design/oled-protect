using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace OLED_Sleeper.Native
{
    // Small managed wrappers around the monitor and dxva2 calls that both the monitor scan and the dimming
    // feature need, so neither has to repeat the enumeration and handle bookkeeping.
    [ExcludeFromCodeCoverage]
    internal static class DisplayMonitors
    {
        // Every display monitor handle on the desktop, in the order Windows enumerates them.
        internal static List<nint> EnumerateHandles()
        {
            var handles = new List<nint>();
            NativeMethods.MonitorEnumCallback collect = (hMonitor, _, _, _) =>
            {
                handles.Add(hMonitor);
                return true;
            };

            NativeMethods.EnumDisplayMonitors(0, 0, collect, 0);
            GC.KeepAlive(collect);
            return handles;
        }

        internal static bool TryDescribe(nint hMonitor, out NativeMethods.MonitorInfoEx description)
        {
            description = new NativeMethods.MonitorInfoEx { cbSize = Marshal.SizeOf<NativeMethods.MonitorInfoEx>() };
            return NativeMethods.GetMonitorInfo(hMonitor, ref description);
        }

        // The handle whose GDI device name (\\.\DISPLAYn) matches, or zero when that display is gone.
        internal static nint FindByDeviceName(string deviceName)
        {
            foreach (nint hMonitor in EnumerateHandles())
            {
                if (TryDescribe(hMonitor, out var description) && description.szDevice == deviceName)
                {
                    return hMonitor;
                }
            }

            return 0;
        }

        // Opens the physical monitors behind a display monitor handle. A duplicated desktop surface has one per
        // panel, and the dxva2 call rejects an array of any other length, so the count is asked first. Null when
        // the handles could not be obtained; otherwise the caller must hand the array to Release.
        internal static NativeMethods.PHYSICAL_MONITOR[]? OpenPhysicalMonitors(nint hMonitor)
        {
            bool counted = NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count);
            var panels = new NativeMethods.PHYSICAL_MONITOR[counted && count > 0 ? count : 1];

            return NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, (uint)panels.Length, panels) ? panels : null;
        }

        internal static void Release(NativeMethods.PHYSICAL_MONITOR[] panels) =>
            NativeMethods.DestroyPhysicalMonitors((uint)panels.Length, panels);

        // One DDC/CI read of the brightness VCP code: the current value and the top of the panel's range.
        internal static bool TryReadBrightness(nint physicalMonitor, out uint current, out uint maximum) =>
            NativeMethods.GetVCPFeatureAndVCPFeatureReply(physicalMonitor, NativeMethods.VCP_CODE_BRIGHTNESS, 0, out current, out maximum);

        internal static bool TryWriteBrightness(nint physicalMonitor, uint value) =>
            NativeMethods.SetVCPFeature(physicalMonitor, NativeMethods.VCP_CODE_BRIGHTNESS, value);

        // Whether the panel hands out a capabilities string, which only a DDC/CI-capable panel does.
        internal static bool ReportsCapabilities(nint physicalMonitor) =>
            NativeMethods.GetCapabilitiesStringLength(physicalMonitor, out _);
    }
}
