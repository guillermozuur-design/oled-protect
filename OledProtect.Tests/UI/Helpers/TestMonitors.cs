using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.UI.Models;
using OledProtect.UI.ViewModels;
using System.Windows;

namespace OledProtect.Tests.UI.Helpers
{
    // Builders for monitor tiles shared by the UI helper and service tests.
    internal static class TestMonitors
    {
        public static MonitorInfo Info(int number, Rect? bounds = null) => new()
        {
            HardwareId = $"MON-{number}",
            DisplayNumber = number,
            DeviceName = $@"\\.\DISPLAY{number}",
            Bounds = bounds ?? new Rect(0, 0, 1920, 1080),
        };

        // An unmanaged tile with factory defaults, drawn 1:1.
        public static MonitorLayoutViewModel Tile(int number)
        {
            var info = Info(number);
            return new MonitorLayoutViewModel(info, 1.0, info.Bounds, 0, 0);
        }

        // A managed tile whose configuration passes validation and is marked as saved.
        public static MonitorLayoutViewModel ValidManagedTile(int number)
        {
            var tile = Tile(number);
            tile.Configuration.ApplySettings(new MonitorSettings
            {
                HardwareId = tile.HardwareId,
                IsManaged = true,
                Behavior = MonitorBehaviorType.Blackout,
                IdleValue = 5,
                IdleUnit = TimeUnit.Minutes,
                IsActiveOnInput = true,
            });
            tile.Configuration.MarkAsSaved();
            return tile;
        }
    }
}
