using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.UI.Services.Interfaces;
using OledProtect.UI.ViewModels;
using System.Collections.ObjectModel;

namespace OledProtect.UI.Services
{
    // Combines three sources into what the settings window shows: the monitors Windows reports,
    // their positions as tiles, and the configuration saved for each (matched by hardware id).
    public class WorkspaceService(
        IMonitorInfoManager monitorSource,
        IMonitorSettingsFileService settingsFile,
        IMonitorLayoutService layout) : IWorkspaceService
    {
        public async Task<ObservableCollection<MonitorLayoutViewModel>> BuildWorkspaceAsync(double canvasWidth, double canvasHeight)
        {
            var monitors = (await monitorSource.GetCurrentMonitorsAsync()).ToList();
            var saved = settingsFile.LoadSettings();

            var tiles = layout.CreateLayout(monitors, canvasWidth, canvasHeight);

            foreach (var tile in tiles)
            {
                // A file edited by hand may list a monitor twice; the first entry wins.
                var settings = saved.Find(s => s.HardwareId == tile.HardwareId);
                if (settings is null)
                    continue;

                // Loaded values are by definition what is on disk, so the tile starts out clean.
                tile.Configuration.ApplySettings(settings);
                tile.Configuration.MarkAsSaved();
            }

            return tiles;
        }

        public async Task<ObservableCollection<MonitorLayoutViewModel>> RefreshWorkspaceAsync(double canvasWidth, double canvasHeight)
        {
            await monitorSource.RefreshMonitorsAsync();
            return await BuildWorkspaceAsync(canvasWidth, canvasHeight);
        }
    }
}
