using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorIdleDetection.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorState.Commands;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorState.Handlers
{
    // After the screen set changes, start from a clean slate: idle tracking off, every affected screen
    // woken up, settings re-joined to the screens that are really there, then tracking back on.
    public class SynchronizeMonitorStateCommandHandler(
        IMonitorIdleDetectionService idleDetection,
        IMonitorSettingsFileService settingsFiles,
        IMonitorDimmingService dimming,
        IMonitorBlackoutService overlays)
        : ICommandHandler<SynchronizeMonitorStateCommand>
    {
        public async Task HandleAsync(SynchronizeMonitorStateCommand command)
        {
            idleDetection.Stop();

            foreach (string id in ScreensToWake(command))
            {
                WakeWithoutWaiting(id);
            }

            List<MonitorSettings> settings = settingsFiles.LoadSettings();
            KeepOnlyConnectedManaged(settings, command.NewMonitors);

            // Wait for the new settings before restarting, so detection never runs on the old ones.
            await idleDetection.UpdateSettingsAsync(settings, command.NewMonitors);
            idleDetection.Start();

            Log.Information("Screen set synchronised; {Count} screen(s) connected.", command.NewMonitors.Count);
        }

        // Everything that was there before, plus whatever just appeared. A newcomer may be a panel that
        // was asleep under an earlier session and still carries an overlay or low brightness.
        private static IEnumerable<string> ScreensToWake(SynchronizeMonitorStateCommand command)
        {
            var before = new HashSet<string>(command.OldMonitors.Select(m => m.HardwareId));
            foreach (MonitorInfo monitor in command.OldMonitors)
            {
                yield return monitor.HardwareId;
            }

            foreach (MonitorInfo monitor in command.NewMonitors)
            {
                if (before.Add(monitor.HardwareId))
                {
                    yield return monitor.HardwareId;
                }
            }
        }

        // Not awaited: brightness over DDC/CI can take seconds and must not hold up the restart. Failures
        // are still logged instead of disappearing as unobserved task exceptions.
        private void WakeWithoutWaiting(string hardwareId)
        {
            _ = LogFailure(overlays.HideBlackoutOverlayAsync(hardwareId), "remove the overlay of", hardwareId);
            _ = LogFailure(dimming.UndimMonitorAsync(hardwareId), "restore the brightness of", hardwareId);
        }

        private static async Task LogFailure(Task work, string what, string hardwareId)
        {
            try
            {
                await work;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "While synchronising, could not {What} screen {HardwareId}.", what, hardwareId);
            }
        }

        // A screen stays managed only while it is connected; its other settings are left untouched.
        private static void KeepOnlyConnectedManaged(List<MonitorSettings> settings, IReadOnlyList<MonitorInfo> connected)
        {
            var connectedIds = connected.Select(m => m.HardwareId).ToHashSet();
            foreach (MonitorSettings setting in settings.Where(s => s.IsManaged))
            {
                setting.IsManaged = connectedIds.Contains(setting.HardwareId);
            }
        }
    }
}
