using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Native;
using Serilog;
using System.Runtime.InteropServices;

namespace OLED_Sleeper.Features.MonitorInformation.Services
{
    /// <summary>
    /// Reads the attached monitors from the Win32 display APIs and probes their panels over DDC/CI.
    /// </summary>
    public sealed class MonitorInfoProvider : IMonitorInfoProvider
    {
        // A panel that just woke up sometimes drops the first DDC/CI transaction, so a read gets a second chance.
        private const int BrightnessReadAttempts = 2;
        private static readonly TimeSpan FailedReadPause = TimeSpan.FromMilliseconds(50);

        public List<MonitorInfo> GetAllMonitorsBasicInfo()
        {
            var identities = IdentifySurfaces();
            var readings = new List<MonitorInfo>();

            foreach (nint hMonitor in DisplayMonitors.EnumerateHandles())
            {
                if (DisplayMonitors.TryDescribe(hMonitor, out var described))
                {
                    readings.Add(BuildReading(hMonitor, described, identities));
                }
            }

            return readings;
        }

        public DdcCiCapabilities GetDdcCiCapabilities(MonitorInfo target)
        {
            var capabilities = ProbeSurface(target.DeviceName);
            Log.Debug("DDC/CI probe of {HardwareId} ({DeviceName}): supported={IsSupported}, brightness range 0-{MaxBrightness}.",
                target.HardwareId, target.DeviceName, capabilities.IsSupported, capabilities.MaxBrightness);
            return capabilities;
        }

        private static MonitorInfo BuildReading(
            nint hMonitor, in NativeMethods.MonitorInfoEx described, Dictionary<string, SurfaceIdentity> identities)
        {
            string deviceName = described.szDevice;
            var identity = identities.GetValueOrDefault(deviceName, SurfaceIdentity.Unresolved);
            if (identity.HardwareId.Length == 0)
            {
                Log.Debug("Could not resolve a hardware ID for {DeviceName}.", deviceName);
            }

            // The HRESULT is not checked: a failed call leaves the DPI at zero, which callers already treat as unknown.
            NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpi, out _);

            return new MonitorInfo
            {
                DeviceName = deviceName,
                HardwareId = identity.HardwareId,
                TopologyLabel = identity.TopologyLabel,
                Bounds = described.rcMonitor.ToWindowsRect(),
                IsPrimary = (described.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                Dpi = dpi,
                DisplayNumber = TrailingNumber(deviceName),
            };
        }

        // \\.\DISPLAY3 -> 3. Anything without trailing digits (or too many of them) -> -1.
        private static int TrailingNumber(string deviceName)
        {
            int firstDigit = deviceName.Length;
            while (firstDigit > 0 && char.IsAsciiDigit(deviceName[firstDigit - 1]))
            {
                firstDigit--;
            }

            bool hasDigits = firstDigit < deviceName.Length;
            return hasDigits && int.TryParse(deviceName.AsSpan(firstDigit), out int number) ? number : -1;
        }

        // ----- DDC/CI probing -----

        private static DdcCiCapabilities ProbeSurface(string deviceName)
        {
            nint hMonitor = DisplayMonitors.FindByDeviceName(deviceName);
            if (hMonitor == 0) return default;

            var panels = DisplayMonitors.OpenPhysicalMonitors(hMonitor);
            if (panels is null) return default;

            try
            {
                // A duplicated surface has several panels; the first one that speaks DDC/CI decides.
                foreach (var panel in panels)
                {
                    if (TryProbePanel(panel.hPhysicalMonitor, out var capabilities)) return capabilities;
                }

                return default;
            }
            finally
            {
                DisplayMonitors.Release(panels);
            }
        }

        // A panel counts as DDC/CI-capable when it answers a brightness read. Some (an LG UltraGear OLED over
        // DisplayPort, for one) never hand out a capabilities string but do take brightness reads and writes,
        // so the capabilities string is only the fallback, and then the range stays unknown.
        private static bool TryProbePanel(nint panel, out DdcCiCapabilities capabilities)
        {
            for (int attempt = 0; attempt < BrightnessReadAttempts; attempt++)
            {
                if (DisplayMonitors.TryReadBrightness(panel, out _, out uint maximum) && maximum > 0)
                {
                    capabilities = new DdcCiCapabilities(true, maximum);
                    return true;
                }

                Thread.Sleep(FailedReadPause);
            }

            capabilities = new DdcCiCapabilities(DisplayMonitors.ReportsCapabilities(panel), 0);
            return capabilities.IsSupported;
        }

        // ----- Identity: which panel a desktop surface belongs to -----

        // Maps each attached adapter's GDI name (\\.\DISPLAYn) to the panel its surface belongs to.
        // Adapters whose panel could not be told apart are left out.
        private static Dictionary<string, SurfaceIdentity> IdentifySurfaces()
        {
            var identities = new Dictionary<string, SurfaceIdentity>();
            List<ActivePath>? activePaths = null; // only duplicated surfaces need these, so read them lazily

            foreach (var adapter in ListDevices(null, 0))
            {
                if (!IsOnDesktop(adapter)) continue;

                var hardwareIds = ListDevices(adapter.DeviceName, 0).ConvertAll(child => child.DeviceID ?? "");

                SurfaceIdentity identity;
                if (hardwareIds.Count == 0)
                {
                    continue;
                }
                else if (hardwareIds.Count == 1)
                {
                    identity = new SurfaceIdentity(hardwareIds[0], "");
                }
                else
                {
                    activePaths ??= ReadActivePaths();
                    identity = IdentifyDuplicatedSurface(adapter.DeviceName, hardwareIds, activePaths);
                }

                if (identity.HardwareId.Length > 0)
                {
                    identities[adapter.DeviceName] = identity;
                }
            }

            return identities;
        }

        // An adapter with several monitor children is a duplicated (cloned) surface. Its children come in no useful
        // order (a virtual display may well be first), so the active display paths decide: the panel the surface's
        // own path drives is the monitor, and every child with a path of its own contributes its display number to
        // the label Windows shows, such as 1|2.
        private static SurfaceIdentity IdentifyDuplicatedSurface(string adapterName, List<string> hardwareIds, List<ActivePath> activePaths)
        {
            string drivenPanelPath = activePaths.Find(path => SameText(path.GdiDeviceName, adapterName)).DevicePath ?? "";
            var interfacePaths = ListDevices(adapterName, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME);

            string owner = "";
            var sharedNumbers = new List<int>();
            int childCount = Math.Min(interfacePaths.Count, hardwareIds.Count);

            for (int child = 0; child < childCount; child++)
            {
                string interfacePath = interfacePaths[child].DeviceID ?? "";
                if (interfacePath.Length == 0) continue;

                if (SameText(interfacePath, drivenPanelPath))
                {
                    owner = hardwareIds[child];
                }

                int pathIndex = activePaths.FindIndex(path => SameText(path.DevicePath, interfacePath));
                if (pathIndex >= 0)
                {
                    sharedNumbers.Add(pathIndex + 1); // display numbers follow the path order, starting at 1
                }
            }

            if (owner.Length == 0)
            {
                owner = hardwareIds[0];
                Log.Debug("{DeviceName} shows {Count} monitors but no active path pointed at one; using the first, {HardwareId}.",
                    adapterName, hardwareIds.Count, owner);
            }

            sharedNumbers.Sort();
            return new SurfaceIdentity(owner, sharedNumbers.Count > 1 ? string.Join('|', sharedNumbers) : "");
        }

        // Walks either the display adapters (parent null) or the monitors attached to one adapter.
        private static List<NativeMethods.DISPLAY_DEVICE> ListDevices(string? parent, uint flags)
        {
            var devices = new List<NativeMethods.DISPLAY_DEVICE>();
            for (uint index = 0; ; index++)
            {
                var device = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
                if (!NativeMethods.EnumDisplayDevices(parent, index, ref device, flags)) return devices;

                devices.Add(device);
            }
        }

        // The active display paths in the order Windows numbers them (the first is display 1); empty when the
        // configuration could not be read.
        private static List<ActivePath> ReadActivePaths()
        {
            const uint scope = NativeMethods.QDC_ONLY_ACTIVE_PATHS;
            if (NativeMethods.GetDisplayConfigBufferSizes(scope, out uint pathCount, out uint modeCount) != 0) return [];

            var pathBuffer = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
            var modeBuffer = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
            if (NativeMethods.QueryDisplayConfig(scope, ref pathCount, pathBuffer, ref modeCount, modeBuffer, 0) != 0) return [];

            var result = new List<ActivePath>((int)pathCount);
            for (int i = 0; i < pathCount; i++)
            {
                result.Add(new ActivePath(SourceGdiName(pathBuffer[i]), TargetDevicePath(pathBuffer[i])));
            }

            return result;
        }

        private static string SourceGdiName(in NativeMethods.DISPLAYCONFIG_PATH_INFO path)
        {
            var request = new NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = RequestHeader<NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(
                    NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, path.sourceAdapterId, path.sourceId),
            };

            return NativeMethods.DisplayConfigGetDeviceInfo(ref request) == 0 ? request.viewGdiDeviceName : "";
        }

        private static string TargetDevicePath(in NativeMethods.DISPLAYCONFIG_PATH_INFO path)
        {
            var request = new NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = RequestHeader<NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME>(
                    NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, path.targetAdapterId, path.targetId),
            };

            return NativeMethods.DisplayConfigGetDeviceInfo(ref request) == 0 ? request.monitorDevicePath : "";
        }

        // The header's size field must hold the size of the whole request packet it heads.
        private static NativeMethods.DISPLAYCONFIG_DEVICE_INFO_HEADER RequestHeader<TPacket>(
            uint requestType, NativeMethods.LUID adapterId, uint id) =>
            new()
            {
                type = requestType,
                size = (uint)Marshal.SizeOf<TPacket>(),
                adapterId = adapterId,
                id = id,
            };

        private static bool IsOnDesktop(in NativeMethods.DISPLAY_DEVICE adapter) =>
            (adapter.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;

        private static bool SameText(string? left, string? right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        // The panel a desktop surface belongs to, plus the display numbers sharing it when it is duplicated.
        private readonly record struct SurfaceIdentity(string HardwareId, string TopologyLabel)
        {
            internal static SurfaceIdentity Unresolved { get; } = new("", "");
        }

        // One active display path: the GDI surface it shows and the device interface path of the panel it drives.
        private readonly record struct ActivePath(string GdiDeviceName, string DevicePath);
    }
}
