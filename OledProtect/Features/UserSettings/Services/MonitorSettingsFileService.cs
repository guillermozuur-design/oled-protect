using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.Storage.Interfaces;
using Serilog;
using Serilog.Events;

namespace OledProtect.Features.UserSettings.Services
{
    // Owns settings.json: { "SchemaVersion": 1, "Monitors": [ ... ] }.
    //
    // Reading never fails loudly. Whatever cannot be used (no file, broken JSON, another schema number,
    // "Monitors": null) simply means "no saved monitors", so the app starts with defaults.
    //
    // Writing is an upsert keyed on HardwareId. The settings window only shows screens that are plugged in,
    // so anything on disk that the caller did not mention is written back untouched.
    public sealed class MonitorSettingsFileService(IAppDataFileStore fileStore) : IMonitorSettingsFileService
    {
        private const string JsonFile = "settings.json";

        // The only schema this build writes or accepts. Raise it when an old file would be misread; new
        // optional properties do not count, they just pick up their default.
        private const int SchemaNumber = 1;

        public event Action<List<MonitorSettings>>? SettingsChanged;

        public List<MonitorSettings> LoadSettings()
        {
            MonitorSettingsDocument? onDisk = fileStore.Read<MonitorSettingsDocument>(JsonFile);
            List<MonitorSettings> usable = onDisk switch
            {
                null => Discard(LogEventLevel.Information, "missing or unreadable"),
                { SchemaVersion: var found } when found != SchemaNumber => Discard(LogEventLevel.Warning, $"schema {found} instead of {SchemaNumber}"),
                { Monitors: null } => [],
                _ => onDisk.Monitors,
            };

            Log.Debug("{File}: {Count} monitor entr(y/ies) in use.", JsonFile, usable.Count);
            return usable;
        }

        public void SaveSettings(List<MonitorSettings> settings)
        {
            try
            {
                List<MonitorSettings> untouched = LoadSettings()
                    .FindAll(stored => !settings.Exists(given => given.HardwareId == stored.HardwareId));

                var document = new MonitorSettingsDocument
                {
                    SchemaVersion = SchemaNumber,
                    Monitors = [.. settings, .. untouched],
                };

                if (!fileStore.TryWrite(JsonFile, document))
                {
                    return;
                }

                Log.Information("{File} written: {Given} given, {Kept} kept for screens not connected.",
                    JsonFile, settings.Count, untouched.Count);
                SettingsChanged?.Invoke(settings);
            }
            catch (Exception error)
            {
                Log.Error(error, "{File} could not be updated.", JsonFile);
            }
        }

        private static List<MonitorSettings> Discard(LogEventLevel level, string why)
        {
            Log.Write(level, "Ignoring {File} ({Reason}); starting from default monitor settings.", JsonFile, why);
            return [];
        }
    }
}
