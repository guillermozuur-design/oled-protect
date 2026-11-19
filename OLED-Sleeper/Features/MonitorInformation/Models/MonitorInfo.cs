using System.Windows;

namespace OLED_Sleeper.Features.MonitorInformation.Models
{
    /// <summary>
    /// A snapshot of one attached monitor: who it is, where it sits and what it can do.
    /// </summary>
    public sealed class MonitorInfo
    {
        /// <summary>
        /// Stable identity of the panel (its PnP hardware ID). Settings are keyed on this, never on
        /// <see cref="DeviceName"/>. Empty when no attached display device could be matched.
        /// </summary>
        public string HardwareId { get; set; } = "";

        /// <summary>
        /// GDI name such as <c>\\.\DISPLAY2</c>. Windows may hand these to other panels when the set of
        /// displays changes, so it only serves to address the monitor right now.
        /// </summary>
        public string DeviceName { get; set; } = "";

        /// <summary>The n in <c>DISPLAYn</c>, or -1 when the device name does not end in a number.</summary>
        public int DisplayNumber { get; set; }

        /// <summary>
        /// For a duplicated desktop surface, the display numbers sharing it as Windows shows them (<c>1|2</c>);
        /// empty otherwise.
        /// </summary>
        public string TopologyLabel { get; set; } = "";

        /// <summary>Desktop area covered by the monitor, in physical pixels.</summary>
        public Rect Bounds { get; set; }

        /// <summary>Effective DPI the monitor's content is scaled to.</summary>
        public uint Dpi { get; set; }

        /// <summary>True for the monitor holding the primary desktop.</summary>
        public bool IsPrimary { get; set; }

        /// <summary>DDC/CI probe result; null as long as the monitor was not probed.</summary>
        public DdcCiCapabilities? Capabilities { get; set; }
    }
}
