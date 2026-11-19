using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorState.Services.Interfaces;
using OledProtect.Features.UserSettings.Commands;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.Features.WorkMode.Services;
using OledProtect.Infrastructure.Hosting.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Infrastructure.Hosting
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
