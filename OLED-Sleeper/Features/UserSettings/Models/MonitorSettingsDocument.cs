namespace OLED_Sleeper.Features.UserSettings.Models
{
    // Root object of settings.json. A file from before versioning has no SchemaVersion key and
    // reads as 0, which never equals a real version, so it is ignored instead of misread.
    public class MonitorSettingsDocument
    {
        public int SchemaVersion { get; set; }

        public List<MonitorSettings> Monitors { get; set; } = [];
    }
}
