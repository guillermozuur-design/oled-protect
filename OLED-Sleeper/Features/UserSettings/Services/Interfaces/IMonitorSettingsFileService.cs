using OLED_Sleeper.Features.UserSettings.Models;

namespace OLED_Sleeper.Features.UserSettings.Services.Interfaces
{
    public interface IMonitorSettingsFileService
    {
        // Fires after a successful save with exactly the list passed to SaveSettings (not the merged file).
        event Action<List<MonitorSettings>>? SettingsChanged;

        // Empty list when there is no usable settings file.
        List<MonitorSettings> LoadSettings();

        // Upserts by HardwareId: monitors absent from the list (e.g. unplugged) keep their stored entry.
        // Swallows and logs every failure, including exceptions from SettingsChanged handlers.
        void SaveSettings(List<MonitorSettings> settings);
    }
}
