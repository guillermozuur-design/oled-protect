using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Native;
using Serilog;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OledProtect.Features.MonitorInformation.Services
{
    /// <summary>
    /// Lists the desktop's monitors through user32 and asks their panels about DDC/CI through dxva2.
    /// </summary>
    public sealed class MonitorInfoProvider : IMonitorInfoProvider
    {
        // Freshly woken panels tend to miss one DDC/CI exchange; each panel gets this many brightness reads.
        private const int ReadsPerPanel = 2;
        private static readonly TimeSpan AfterMissedRead = TimeSpan.FromMilliseconds(50);

        public List<MonitorInfo> GetAllMonitorsBasicInfo()
        {
            var owners = MapSurfacesToPanels();

            return DisplayMonitors.EnumerateHandles()
                .Select(handle => DisplayMonitors.TryDescribe(handle, out var info) ? Describe(handle, info, owners) : null)
                .OfType<MonitorInfo>()
                .ToList();
        }

        public DdcCiCapabilities GetDdcCiCapabilities(MonitorInfo target)
        {
            var found = AskSurface(target.DeviceName);
            Log.Debug("{DeviceName} / {HardwareId}: DDC/CI {IsSupported}, top level {MaxBrightness}.",
                target.DeviceName, target.HardwareId, found.IsSupported, found.MaxBrightness);
            return found;
        }

        private static MonitorInfo Describe(nint handle, NativeMethods.MonitorInfoEx info, Dictionary<string, PanelOwner> owners)
        {
            string surface = info.szDevice;
            if (!owners.TryGetValue(surface, out var owner))
            {
                owner = new PanelOwner("", "");
                Log.Debug("{DeviceName}: no panel could be matched to this surface.", surface);
            }

            // A failing call leaves dpi at 0, which the rest of the app reads as "unknown"; no need to check it.
            _ = NativeMethods.GetDpiForMonitor(handle, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpi, out _);

            return new MonitorInfo
            {
                DeviceName = surface,
                DisplayNumber = NumberSuffix(surface),
                HardwareId = owner.HardwareId,
                TopologyLabel = owner.Label,
                IsPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0,
                Bounds = info.rcMonitor.ToWindowsRect(),
                Dpi = dpi,
            };
        }

        // "\\.\DISPLAY12" gives 12; a name that does not end in digits (or whose digits overflow an int) gives -1.
        private static int NumberSuffix(string name)
        {
            ReadOnlySpan<char> whole = name;
            ReadOnlySpan<char> digits = whole[whole.TrimEnd("0123456789").Length..];
            return !digits.IsEmpty && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        // ===== DDC/CI =====

        private static DdcCiCapabilities AskSurface(string surface)
        {
            nint handle = DisplayMonitors.FindByDeviceName(surface);
            var physical = handle == 0 ? null : DisplayMonitors.OpenPhysicalMonitors(handle);
            if (physical is null) return default;

            try
            {
                // A cloned surface has a physical monitor per panel; the first that answers wins.
                return physical
                    .Select(p => AskPanel(p.hPhysicalMonitor))
                    .FirstOrDefault(answer => answer is not null) ?? default;
            }
            finally
            {
                DisplayMonitors.Release(physical);
            }
        }

        // Brightness reads come first because some panels (seen on an LG UltraGear OLED via DisplayPort) work
        // fine with them yet never return a capabilities string. That string is the last resort and gives no range.
        // Null when the panel did not respond to either.
        private static DdcCiCapabilities? AskPanel(nint panel)
        {
            for (int left = ReadsPerPanel; left > 0; left--)
            {
                if (DisplayMonitors.TryReadBrightness(panel, out _, out uint top) && top > 0) return new DdcCiCapabilities(true, top);
                Thread.Sleep(AfterMissedRead);
            }

            return DisplayMonitors.ReportsCapabilities(panel) ? new DdcCiCapabilities(true, 0) : null;
        }

        // ===== Which panel owns which desktop surface =====

        // Keyed by the adapter's GDI name (\\.\DISPLAYn). Surfaces whose panel stays unknown are not in the map.
        private static Dictionary<string, PanelOwner> MapSurfacesToPanels()
        {
            var owners = new Dictionary<string, PanelOwner>();
            Lazy<ActiveTopology> topology = new(ActiveTopology.Read); // only clones need it

            var onDesktop = EnumerateDevices(null, 0).Where(a => (a.StateFlags & NativeMethods.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0);
            foreach (var adapter in onDesktop)
            {
                string[] ids = EnumerateDevices(adapter.DeviceName, 0).Select(m => m.DeviceID ?? "").ToArray();
                PanelOwner? owner = ids.Length switch
                {
                    0 => null,
                    1 => new PanelOwner(ids[0], ""),
                    _ => OwnerOfClone(adapter.DeviceName, ids, topology.Value),
                };

                if (owner is { HardwareId.Length: > 0 }) owners[adapter.DeviceName] = owner;
            }

            return owners;
        }

        // A cloned surface lists several monitors under one adapter, in arbitrary order. The CCD path whose source
        // is this surface names the panel that really drives it (by device interface path); EnumDisplayDevices
        // with EDD_GET_DEVICE_INTERFACE_NAME gives the same kind of path per listed monitor, so the two can be
        // matched. The label lists the display numbers of every listed monitor that has an active path ("1|2").
        private static PanelOwner OwnerOfClone(string surface, string[] ids, ActiveTopology topology)
        {
            string driver = topology.TargetOfSource(surface);
            var members = ids
                .Zip(EnumerateDevices(surface, NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME).Select(m => m.DeviceID ?? ""))
                .Where(pair => pair.Second.Length > 0)
                .ToList();

            string? matched = members.LastOrDefault(pair => SameText(pair.Second, driver)).First;
            string owner = string.IsNullOrEmpty(matched) ? ids[0] : matched;
            if (string.IsNullOrEmpty(matched))
            {
                Log.Debug("{DeviceName}: clone of {Count} monitors without a driving path match; taking {HardwareId}.",
                    surface, ids.Length, owner);
            }

            var numbers = members.Select(pair => topology.NumberOfTarget(pair.Second)).Where(n => n > 0).Order().ToList();
            return new PanelOwner(owner, numbers.Count > 1 ? string.Join('|', numbers) : "");
        }

        // Adapters when parent is null, otherwise the monitors listed under that adapter.
        private static IEnumerable<NativeMethods.DISPLAY_DEVICE> EnumerateDevices(string? parent, uint flags)
        {
            uint i = 0;
            while (TryGet(i++, out var device)) yield return device;

            // A fresh struct per index, with cb filled in as the API requires.
            bool TryGet(uint index, out NativeMethods.DISPLAY_DEVICE found)
            {
                found = new NativeMethods.DISPLAY_DEVICE { cb = Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
                return NativeMethods.EnumDisplayDevices(parent, index, ref found, flags);
            }
        }

        private static bool SameText(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // The surface's owning panel and, for clones, the "1|2" style label Windows shows.
        private sealed record PanelOwner(string HardwareId, string Label);

        /// <summary>
        /// The active CCD paths (QueryDisplayConfig with QDC_ONLY_ACTIVE_PATHS) reduced to two lookups:
        /// source GDI name to target device path, and target device path to display number. Windows numbers the
        /// displays in path order from 1. When a name occurs on more than one path, the first path counts.
        /// </summary>
        private sealed class ActiveTopology
        {
            private readonly Dictionary<string, string> _targetBySource = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _numberByTarget = new(StringComparer.OrdinalIgnoreCase);

            public string TargetOfSource(string gdiName) => _targetBySource.GetValueOrDefault(gdiName, "");

            public int NumberOfTarget(string devicePath) => _numberByTarget.GetValueOrDefault(devicePath);

            // Empty when the configuration cannot be queried.
            public static ActiveTopology Read()
            {
                var topology = new ActiveTopology();
                const uint flags = NativeMethods.QDC_ONLY_ACTIVE_PATHS;

                if (NativeMethods.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount) != 0) return topology;

                var pathBuffer = new NativeMethods.DISPLAYCONFIG_PATH_INFO[pathCount];
                var modeBuffer = new NativeMethods.DISPLAYCONFIG_MODE_INFO[modeCount];
                if (NativeMethods.QueryDisplayConfig(flags, ref pathCount, pathBuffer, ref modeCount, modeBuffer, 0) != 0) return topology;

                foreach (var (path, number) in pathBuffer.Take((int)pathCount).Select((p, i) => (p, i + 1)))
                {
                    string target = TargetPath(path);
                    topology._targetBySource.TryAdd(SourceName(path), target);
                    topology._numberByTarget.TryAdd(target, number);
                }

                return topology;
            }

            private static string SourceName(NativeMethods.DISPLAYCONFIG_PATH_INFO path)
            {
                var packet = new NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = Header<NativeMethods.DISPLAYCONFIG_SOURCE_DEVICE_NAME>(
                        NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME, path.sourceAdapterId, path.sourceId),
                };
                return NativeMethods.DisplayConfigGetDeviceInfo(ref packet) == 0 ? packet.viewGdiDeviceName ?? "" : "";
            }

            private static string TargetPath(NativeMethods.DISPLAYCONFIG_PATH_INFO path)
            {
                var packet = new NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = Header<NativeMethods.DISPLAYCONFIG_TARGET_DEVICE_NAME>(
                        NativeMethods.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME, path.targetAdapterId, path.targetId),
                };
                return NativeMethods.DisplayConfigGetDeviceInfo(ref packet) == 0 ? packet.monitorDevicePath ?? "" : "";
            }

            // DisplayConfigGetDeviceInfo wants the size of the complete packet in its header.
            private static NativeMethods.DISPLAYCONFIG_DEVICE_INFO_HEADER Header<TPacket>(uint kind, NativeMethods.LUID adapter, uint id) =>
                new() { type = kind, size = (uint)Marshal.SizeOf<TPacket>(), adapterId = adapter, id = id };
        }
    }
}
