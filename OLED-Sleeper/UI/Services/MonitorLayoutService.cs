using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.UI.Services.Interfaces;
using OLED_Sleeper.UI.ViewModels;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows;

namespace OLED_Sleeper.UI.Services
{
    // Draws the desktop as a miniature: the bounding box of all monitors is scaled uniformly to fill
    // 90% of the drawing area (leaving a margin around it) and centred in it.
    public class MonitorLayoutService : IMonitorLayoutService
    {
        private const double FillFraction = 0.9;

        public ObservableCollection<MonitorLayoutViewModel> CreateLayout(List<MonitorInfo>? monitors, double areaWidth, double areaHeight)
        {
            var tiles = new ObservableCollection<MonitorLayoutViewModel>();
            if (monitors is null || monitors.Count == 0)
            {
                Log.Warning("Monitor layout requested without any monitors; nothing to draw.");
                return tiles;
            }

            Rect desktop = DesktopBounds(monitors);
            double scale = FillFraction * Math.Min(areaWidth / desktop.Width, areaHeight / desktop.Height);
            double marginX = (areaWidth - desktop.Width * scale) / 2;
            double marginY = (areaHeight - desktop.Height * scale) / 2;

            Log.Debug("Layout of {Count} monitor(s): desktop {Desktop} into {W}x{H}, scale {Scale}, margin ({MX}, {MY})",
                monitors.Count, desktop, areaWidth, areaHeight, scale, marginX, marginY);

            foreach (var monitor in monitors)
            {
                var tile = new MonitorLayoutViewModel(monitor, scale, desktop, marginX, marginY);
                Log.Debug("  {Device} {Bounds} @ {Dpi} dpi -> tile ({X}, {Y}) {TW}x{TH}",
                    monitor.DeviceName, monitor.Bounds, monitor.Dpi,
                    tile.ScaledLeft, tile.ScaledTop, tile.ScaledWidth, tile.ScaledHeight);
                tiles.Add(tile);
            }

            return tiles;
        }

        // The smallest rectangle covering every monitor, in virtual-desktop pixels.
        private static Rect DesktopBounds(IEnumerable<MonitorInfo> monitors)
        {
            Rect union = Rect.Empty;
            foreach (var monitor in monitors)
                union.Union(monitor.Bounds);
            return union;
        }
    }
}
