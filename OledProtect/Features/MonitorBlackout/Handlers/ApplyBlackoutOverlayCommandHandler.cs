using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorBlackout.Handlers
{
    // Puts the overlay up and, when asked and the panel speaks DDC/CI, turns the backlight to zero at the
    // same time. Never throws: a failed blackout is logged and the screen simply stays as it was.
    public class ApplyBlackoutOverlayCommandHandler(IMonitorInfoManager screens, IMonitorBlackoutService overlays, IMonitorDimmingService dimmer)
        : ICommandHandler<ApplyBlackoutOverlayCommand>
    {
        public async Task HandleAsync(ApplyBlackoutOverlayCommand command)
        {
            try
            {
                MonitorInfo? target = await FindMonitorAsync(command.HardwareId);
                if (target is null)
                {
                    Log.Warning("Blackout skipped: screen {HardwareId} is not connected (any more).", command.HardwareId);
                    return;
                }

                Log.Information("Blacking out screen {HardwareId} (scene {Scene}).", target.HardwareId, command.Scene);
                await Task.WhenAll(StartWork(target, command));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Blackout of screen {HardwareId} failed.", command.HardwareId);
            }
        }

        private async Task<MonitorInfo?> FindMonitorAsync(string hardwareId)
        {
            var connected = await screens.GetCurrentMonitorsAsync();
            return connected.FirstOrDefault(m => m.HardwareId == hardwareId);
        }

        // The overlay always goes up; the backlight only follows when that was asked for and is possible.
        private IEnumerable<Task> StartWork(MonitorInfo target, ApplyBlackoutOverlayCommand command)
        {
            yield return overlays.ShowBlackoutOverlayAsync(target.HardwareId, target.Bounds, command.Scene);

            bool canDim = target.Capabilities?.IsSupported == true;
            if (command.LowerBrightness && canDim)
            {
                yield return dimmer.DimMonitorAsync(target.HardwareId, 0);
            }
            else if (command.LowerBrightness)
            {
                Log.Debug("Screen {HardwareId} has no DDC/CI; keeping its brightness during blackout.", target.HardwareId);
            }
        }
    }
}
