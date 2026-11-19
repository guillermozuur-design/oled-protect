using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorIdleDetection.Services.Interfaces;
using OLED_Sleeper.Features.MonitorState.Services.Interfaces;
using OLED_Sleeper.Features.UserSettings.Commands;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using OLED_Sleeper.Features.WorkMode.Services;
using OLED_Sleeper.Infrastructure.Hosting.Interfaces;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Infrastructure.Hosting
{
    // Wires the long-running monitor services together. Brightness is reset to normal both at start (to
    // undo a previous run that crashed while a screen was dimmed) and at stop (so nothing stays dark).
    public sealed class ApplicationOrchestrator(
        IMediator bus,
        IMonitorIdleDetectionService idleDetection,
        IMonitorSettingsFileService settingsFile,
        IMonitorStateWatcher displayWatcher,
        IWorkModeService workMode,
        IBrightnessScheduleService schedule) : IApplicationOrchestrator
    {
        public void Start()
        {
            _ = ResetBrightnessAsync();
            Subscribe(true);
            displayWatcher.Start();
            schedule.Start();
        }

        public async Task StopAsync()
        {
            Log.Information("Stopping monitor services.");

            Subscribe(false);
            idleDetection.Stop();
            displayWatcher.Stop();
            schedule.Stop();

            await ResetBrightnessAsync();
        }

        private void Subscribe(bool attach)
        {
            if (attach)
            {
                settingsFile.SettingsChanged += ForwardSavedSettings;
                workMode.Changed += SyncWorkMode;
            }
            else
            {
                settingsFile.SettingsChanged -= ForwardSavedSettings;
                workMode.Changed -= SyncWorkMode;
            }
        }

        private void ForwardSavedSettings(List<MonitorSettings> saved) =>
            _ = bus.SendAsync(new ApplySettingsChangeCommand { Settings = saved });

        // The flag in the event can be stale: a timer expiry and a user click may raise their events out
        // of order on different threads. Asking the service for its current state avoids that race.
        private void SyncWorkMode(bool _) => idleDetection.SetPaused(workMode.IsEnabled);

        private async Task ResetBrightnessAsync()
        {
            Log.Information("Resetting every monitor to its normal brightness.");
            try
            {
                await bus.SendAsync(new RestoreBrightnessOnAllMonitorsCommand());
                Log.Information("Brightness reset finished.");
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Brightness reset failed.");
            }
        }
    }
}
