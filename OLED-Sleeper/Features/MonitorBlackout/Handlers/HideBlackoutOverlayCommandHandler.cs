using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.MonitorBlackout.Handlers
{
    // Wake-up path: must never throw, or one broken overlay could stop the rest of a restore.
    public class HideBlackoutOverlayCommandHandler(IMonitorBlackoutService overlays)
        : ICommandHandler<HideBlackoutOverlayCommand>
    {
        public async Task HandleAsync(HideBlackoutOverlayCommand command)
        {
            Log.Information("Removing overlay from screen {HardwareId}.", command.HardwareId);
            try
            {
                await overlays.HideBlackoutOverlayAsync(command.HardwareId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Removing the overlay from screen {HardwareId} failed.", command.HardwareId);
            }
        }
    }
}
