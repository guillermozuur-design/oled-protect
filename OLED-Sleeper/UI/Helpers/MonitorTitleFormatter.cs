using OLED_Sleeper.Features.MonitorInformation.Models;

namespace OLED_Sleeper.UI.Helpers
{
    // The name a monitor gets in the UI, numbered like Windows' own display settings.
    public static class MonitorTitleFormatter
    {
        // A duplicated (cloned) surface carries a topology label such as "1|2" listing every panel that
        // shows it; a normal monitor just uses its display number.
        public static string Format(MonitorInfo monitor)
        {
            var label = monitor.TopologyLabel is { Length: > 0 } topology
                ? topology
                : monitor.DisplayNumber.ToString();

            var title = "Scherm " + label;
            return monitor.IsPrimary ? title + " (hoofdscherm)" : title;
        }
    }
}
