using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.UserSettings.Models;

namespace OLED_Sleeper.Features.MonitorIdleDetection.Services.Interfaces
{
    // Decides, per managed screen, when it has gone idle and when it is in use again.
    public interface IMonitorIdleDetectionService
    {
        // (Re)starts the background check; restarting replaces a running one.
        void Start();

        // Ends the background check. Calling it twice is fine.
        void Stop();

        // Work mode. Pausing wakes every managed screen at once and keeps them awake; resuming starts
        // every idle timer from zero.
        void SetPaused(bool paused);

        // Replaces the screens being tracked. Only managed settings with a matching connected screen are
        // kept; the screen list supplies the geometry, so callers pass the one they already hold instead
        // of this service reading a possibly stale cache. Completes once the new set is in effect.
        Task UpdateSettingsAsync(List<MonitorSettings> monitorSettings, IReadOnlyList<MonitorInfo> monitors);
    }
}
