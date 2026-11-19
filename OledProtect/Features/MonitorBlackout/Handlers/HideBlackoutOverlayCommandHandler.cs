using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorBlackout.Handlers
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
