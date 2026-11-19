using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Storage.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.UserSettings.Services
{
    public sealed class MonitorSettingsFileService(IAppDataFileStore fileStore) : IMonitorSettingsFileService
    {
        private const string FileName = "settings.json";

        // Bump only when an old file would be interpreted wrongly (added fields just take their defaults).
        // A file with another version is dropped rather than migrated.
        private const int Version = 1;

        public event Action<List<MonitorSettings>>? SettingsChanged;

        public List<MonitorSettings> LoadSettings()
        {
            var stored = fileStore.Read<MonitorSettingsDocument>(FileName);

            switch (stored)
            {
                case null:
                    Log.Information("No usable {File}; using default monitor settings.", FileName);
                    return [];

                case { SchemaVersion: not Version }:
                    Log.Warning("{File} has schema version {Found}, expected {Expected}; using defaults.",
                        FileName, stored.SchemaVersion, Version);
                    return [];

                default:
                    // A hand-edited "Monitors": null must not crash startup.
                    var monitors = stored.Monitors ?? [];
                    Log.Information("Read settings for {Count} monitor(s).", monitors.Count);
                    return monitors;
            }
        }

        public void SaveSettings(List<MonitorSettings> changed)
        {
            try
            {
                var toWrite = new MonitorSettingsDocument { SchemaVersion = Version, Monitors = Upsert(changed) };
                if (!fileStore.TryWrite(FileName, toWrite))
                    return;

                Log.Information("Wrote settings for {Count} monitor(s).", toWrite.Monitors.Count);
                SettingsChanged?.Invoke(changed);
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Saving monitor settings failed.");
            }
        }

        // The editor only knows the monitors that are connected right now; entries for the others are
        // carried over from disk so unplugging a screen does not wipe its configuration.
        private List<MonitorSettings> Upsert(List<MonitorSettings> incoming)
        {
            var result = new List<MonitorSettings>(incoming);
            var seen = new HashSet<string>(incoming.Select(m => m.HardwareId));

            int carried = 0;
            foreach (var old in LoadSettings())
            {
                if (seen.Contains(old.HardwareId))
                    continue;
                result.Add(old);
                carried++;
            }

            if (carried > 0)
                Log.Information("Carried over stored settings of {Count} disconnected monitor(s).", carried);

            return result;
        }
    }
}
