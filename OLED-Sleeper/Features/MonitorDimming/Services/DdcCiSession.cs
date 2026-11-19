using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Native;
using System.Diagnostics.CodeAnalysis;

namespace OLED_Sleeper.Features.MonitorDimming.Services
{
    /// <summary>
    /// A DDC/CI channel over one dxva2 physical monitor handle.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal sealed class DdcCiSession : IDdcCiSession
    {
        // dxva2 hands out and takes back the physical monitors of one HMONITOR as a set, so the whole set is kept
        // even though only one of them is used.
        private NativeMethods.PHYSICAL_MONITOR[]? _ownedPanels;
        private readonly nint _panel;

        internal DdcCiSession(NativeMethods.PHYSICAL_MONITOR[] ownedPanels, int panelIndex)
        {
            _ownedPanels = ownedPanels;
            _panel = ownedPanels[panelIndex].hPhysicalMonitor;
        }

        public uint? GetBrightness() =>
            DisplayMonitors.TryReadBrightness(_panel, out uint current, out _) ? current : null;

        public bool SetBrightness(uint rawValue) => DisplayMonitors.TryWriteBrightness(_panel, rawValue);

        // Safe to call more than once: only the first call releases the handles.
        public void Dispose()
        {
            var panels = Interlocked.Exchange(ref _ownedPanels, null);
            if (panels is not null)
            {
                DisplayMonitors.Release(panels);
            }
        }
    }
}
