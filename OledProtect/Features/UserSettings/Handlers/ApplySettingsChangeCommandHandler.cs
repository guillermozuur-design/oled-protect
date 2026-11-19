using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.UserSettings.Commands;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.UserSettings.Handlers
{
    // Two phases, strictly in this order:
    //   1. hand the saved rules to idle detection (with the screen list it needs for geometry);
    //   2. wake every screen that was saved, all at the same time.
    // Phase 2 waits for phase 1 so a woken screen is never judged by its old timeout and dimmed again.
    // The handler completes when every wake-up has finished; a failure anywhere is logged, not thrown.
    public sealed class ApplySettingsChangeCommandHandler(
        IMediator bus,
        IMonitorIdleDetectionService idleDetection,
        IMonitorInfoManager screens) : ICommandHandler<ApplySettingsChangeCommand>
    {
        public async Task HandleAsync(ApplySettingsChangeCommand command)
        {
            List<MonitorSettings> saved = command.Settings;
            try
            {
                await idleDetection.UpdateSettingsAsync(saved, await screens.GetCurrentMonitorsAsync());

                Task[] wakeUps = [.. saved.Select(entry => Wake(entry.HardwareId))];
                await Task.WhenAll(wakeUps);
                Log.Information("Settings change applied; {Count} screen(s) woken.", wakeUps.Length);
            }
            catch (Exception error)
            {
                Log.Error(error, "Could not put the new monitor settings into effect.");
            }
        }

        private Task Wake(string hardwareId)
        {
            Log.Debug("Waking {HardwareId} because its settings were saved.", hardwareId);
            return bus.SendAsync(new RestoreMonitorStateCommand { HardwareId = hardwareId });
        }
    }
}
