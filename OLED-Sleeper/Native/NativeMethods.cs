using System.Runtime.InteropServices;
using System.Text;

namespace OLED_Sleeper.Native
{
    // The Win32 surface this app talks to, and nothing more. Member names follow the Windows SDK so they can be
    // looked up there. Scalar-only signatures use the LibraryImport source generator; signatures that carry
    // inline strings, string builders or callbacks stay on classic DllImport marshalling.
    internal static partial class NativeMethods
    {
        private const string User32 = "user32.dll";
        private const string Dxva2 = "dxva2.dll";
        private const string Shcore = "shcore.dll";
        private const string DwmApi = "dwmapi.dll";

        // Character counts of the fixed-size text buffers inside the structures below.
        private const int CCHDEVICENAME = 32;
        private const int DescriptionChars = 128;
        private const int FriendlyNameChars = 64;

        // ---------------------------------------------------------------------------------------------
        // Shared value types
        // ---------------------------------------------------------------------------------------------

        // RECT: four edges in physical pixels.
        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        // POINT: a cursor position in physical pixels.
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;
        }

        // LUID: identifies a display adapter for the CCD (QueryDisplayConfig) calls.
        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            internal uint LowPart;
            internal int HighPart;
        }

        // ---------------------------------------------------------------------------------------------
        // Input and windows (user32, dwmapi)
        // ---------------------------------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        internal struct LASTINPUTINFO
        {
            internal uint cbSize;
            internal uint dwTime;
        }

        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_NOACTIVATE = 0x0800_0000;

        internal static readonly nint HWND_TOPMOST = -1;
        internal const uint SWP_NOSIZE = 0x01;
        internal const uint SWP_NOMOVE = 0x02;
        internal const uint SWP_NOACTIVATE = 0x10;

        // Window bounds without the invisible resize border Windows 10+ adds around top-level windows.
        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [LibraryImport(User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [LibraryImport(User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport(User32)]
        internal static partial nint GetForegroundWindow();

        [LibraryImport(User32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

        [DllImport(User32, EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

        // Only the 64-bit export exists under these names; the app ships as x64.
        [LibraryImport(User32, EntryPoint = "GetWindowLongPtrW")]
        internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

        [LibraryImport(User32, EntryPoint = "SetWindowLongPtrW")]
        internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport(User32, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [LibraryImport(DwmApi)]
        internal static partial int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

        // ---------------------------------------------------------------------------------------------
        // Monitors and display devices (user32, shcore)
        // ---------------------------------------------------------------------------------------------

        internal const uint MONITORINFOF_PRIMARY = 1;
        internal const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 1;

        // Makes EnumDisplayDevices report a monitor's device interface path in DeviceID instead of its hardware ID.
        internal const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;

        // GetDpiForMonitor: the DPI Windows scales the monitor's content to.
        internal const int MDT_EFFECTIVE_DPI = 0;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct MonitorInfoEx
        {
            internal int cbSize;
            internal Rect rcMonitor;
            internal Rect rcWork;
            internal uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            internal string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAY_DEVICE
        {
            internal int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            internal string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DescriptionChars)]
            internal string DeviceString;
            internal uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DescriptionChars)]
            internal string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DescriptionChars)]
            internal string DeviceKey;
        }

        // The RECT pointer is passed through as a raw address because no caller needs it.
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate bool MonitorEnumCallback(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumCallback lpfnEnum, nint dwData);

        [DllImport(User32, EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

        [DllImport(User32, EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [LibraryImport(Shcore)]
        internal static partial int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        // ---------------------------------------------------------------------------------------------
        // Display configuration, CCD API (user32)
        // ---------------------------------------------------------------------------------------------

        internal const uint QDC_ONLY_ACTIVE_PATHS = 0x2;
        internal const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
        internal const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

        // DISPLAYCONFIG_PATH_INFO, flattened. Only the adapter/id pairs of the source and the target are read;
        // the rest of the 72-byte record is covered by the explicit size.
        [StructLayout(LayoutKind.Sequential, Size = 72)]
        internal struct DISPLAYCONFIG_PATH_INFO
        {
            internal LUID sourceAdapterId;
            internal uint sourceId;
            internal uint sourceModeInfoIdx;
            internal uint sourceStatusFlags;
            internal LUID targetAdapterId;
            internal uint targetId;
        }

        // DISPLAYCONFIG_MODE_INFO is only needed as a buffer of the right size: a 16-byte head and a 48-byte union.
        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal struct DISPLAYCONFIG_MODE_INFO
        {
            internal uint infoType;
            internal uint id;
            internal LUID adapterId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            internal uint type;
            internal uint size;
            internal LUID adapterId;
            internal uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            internal DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            internal string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            internal DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            internal uint flags;
            internal uint outputTechnology;
            internal ushort edidManufactureId;
            internal ushort edidProductCodeId;
            internal uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = FriendlyNameChars)]
            internal string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DescriptionChars)]
            internal string monitorDevicePath;
        }

        [LibraryImport(User32)]
        internal static partial int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, nint currentTopologyId);

        // One native function, two request packet shapes.
        [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport(User32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

        // ---------------------------------------------------------------------------------------------
        // DDC/CI over the monitor configuration API (dxva2)
        // ---------------------------------------------------------------------------------------------

        // MCCS VCP code for luminance.
        internal const byte VCP_CODE_BRIGHTNESS = 0x10;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct PHYSICAL_MONITOR
        {
            internal nint hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DescriptionChars)]
            internal string szPhysicalMonitorDescription;
        }

        [LibraryImport(Dxva2)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport(Dxva2, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport(Dxva2, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        // pvct receives the VCP code type; callers that do not care pass zero.
        [LibraryImport(Dxva2)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetVCPFeatureAndVCPFeatureReply(nint hMonitor, byte bVCPCode, nint pvct, out uint pdwCurrentValue, out uint pdwMaximumValue);

        [LibraryImport(Dxva2)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetVCPFeature(nint hMonitor, byte bVCPCode, uint dwNewValue);

        [LibraryImport(Dxva2)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCapabilitiesStringLength(nint hMonitor, out uint pdwCapabilitiesStringLengthInCharacters);
    }
}
