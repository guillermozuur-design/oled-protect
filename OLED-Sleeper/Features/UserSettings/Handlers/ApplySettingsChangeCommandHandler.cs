using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorIdleDetection.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.UserSettings.Commands;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.UserSettings.Handlers
{
    public sealed class ApplySettingsChangeCommandHandler(IMediator bus, IMonitorIdleDetectionService idleDetection, IMonitorInfoManager screens)
        : ICommandHandler<ApplySettingsChangeCommand>
    {
        public Task HandleAsync(ApplySettingsChangeCommand command) => ApplyAsync(command.Settings);

        private async Task ApplyAsync(List<MonitorSettings> saved)
        {
            try
            {
                // Idle detection must know the new rules before any screen is woken, otherwise a restored
                // monitor could immediately be put back to sleep under the old timeout.
                var knownMonitors = await screens.GetCurrentMonitorsAsync();
                await idleDetection.UpdateSettingsAsync(saved, knownMonitors);

                foreach (string id in saved.Select(s => s.HardwareId))
                    WakeMonitor(id);
            }
            catch (Exception failure)
            {
                Log.Error(failure, "Applying the saved monitor settings failed.");
            }
        }

        // Fire-and-forget: one slow DDC/CI monitor should not hold up the others.
        private void WakeMonitor(string hardwareId)
        {
            _ = bus.SendAsync(new RestoreMonitorStateCommand { HardwareId = hardwareId });
            Log.Information("Requested restore of {HardwareId} after a settings change.", hardwareId);
        }
    }
}
