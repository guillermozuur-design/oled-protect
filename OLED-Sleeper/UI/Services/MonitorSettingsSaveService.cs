using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.UI.Helpers;
using OLED_Sleeper.UI.Services.Interfaces;
using OLED_Sleeper.UI.ViewModels;

namespace OLED_Sleeper.UI.Services
{
    // Validate first, then write all monitors in one go. A rejected save shows a dialog instead.
    public class MonitorSettingsSaveService(
        IMonitorSettingsFileService settingsFile,
        IDialogService dialogs) : IMonitorSettingsSaveService
    {
        private const string RejectedTitle = "Instellingen niet compleet";

        public bool TrySave(IReadOnlyList<MonitorLayoutViewModel> monitors)
        {
            if (MonitorSettingsValidator.BuildValidationError(monitors) is { } problem)
            {
                dialogs.ShowError(problem, RejectedTitle);
                return false;
            }

            var snapshot = new List<MonitorSettings>(monitors.Count);
            foreach (var tile in monitors)
                snapshot.Add(tile.Configuration.ToSettings());

            settingsFile.SaveSettings(snapshot);

            // Only after the write succeeded: if it throws, the edits stay marked as unsaved.
            foreach (var tile in monitors)
                tile.Configuration.MarkAsSaved();

            return true;
        }
    }
}
