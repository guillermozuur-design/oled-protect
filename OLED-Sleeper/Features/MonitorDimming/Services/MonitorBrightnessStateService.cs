using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Storage.Interfaces;

namespace OLED_Sleeper.Features.MonitorDimming.Services
{
    /// <summary>
    /// Stores the pre-dim brightness map as JSON in the app's data folder.
    /// </summary>
    public sealed class MonitorBrightnessStateService(IAppDataFileStore fileStore) : IMonitorBrightnessStateService
    {
        // Existing installations already have this file; renaming it would lose their recordings.
        private const string FileName = "brightness_state.json";

        public Dictionary<string, uint> LoadState() => fileStore.Read<Dictionary<string, uint>>(FileName) ?? [];

        public void SaveState(Dictionary<string, uint> snapshot) => fileStore.TryWrite(FileName, snapshot);
    }
}
