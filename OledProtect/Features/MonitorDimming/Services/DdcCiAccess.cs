using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Native;
using System.Diagnostics.CodeAnalysis;

namespace OledProtect.Features.MonitorDimming.Services
{
    /// <summary>
    /// Opens real DDC/CI channels through dxva2. All native work of the dimming feature sits behind this class
    /// and <see cref="DdcCiSession"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class DdcCiAccess : IDdcCiAccess
    {
        public IDdcCiSession? OpenSession(string gdiDeviceName)
        {
            nint hMonitor = DisplayMonitors.FindByDeviceName(gdiDeviceName);
            if (hMonitor == 0) return null;

            var panels = DisplayMonitors.OpenPhysicalMonitors(hMonitor);
            if (panels is null) return null;

            int target = panels.Length == 1 ? 0 : IndexOfAnsweringPanel(panels);
            if (target >= 0)
            {
                return new DdcCiSession(panels, target);
            }

            DisplayMonitors.Release(panels);
            return null;
        }

        // A duplicated surface yields one physical monitor per panel, in no useful order, and a virtual display can
        // come first. The first panel that answers a brightness read is the one to talk to.
        private static int IndexOfAnsweringPanel(NativeMethods.PHYSICAL_MONITOR[] panels) =>
            Array.FindIndex(panels, panel => DisplayMonitors.TryReadBrightness(panel.hPhysicalMonitor, out _, out _));
    }
}
