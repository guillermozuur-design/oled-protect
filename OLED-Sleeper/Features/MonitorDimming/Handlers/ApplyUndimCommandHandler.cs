using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.MonitorDimming.Handlers
{
    /// <summary>
    /// Hands an undim request to the dimming service. A failure is logged, not thrown, so waking one monitor
    /// cannot break the activity handling of the others.
    /// </summary>
    public sealed class ApplyUndimCommandHandler(IMonitorDimmingService dimming) : ICommandHandler<ApplyUndimCommand>
    {
        public async Task HandleAsync(ApplyUndimCommand request)
        {
            Log.Information("Restoring the brightness of monitor {HardwareId}.", request.HardwareId);
            try
            {
                await dimming.UndimMonitorAsync(request.HardwareId);
            }
            catch (Exception error)
            {
                Log.Error(error, "Restoring the brightness of monitor {HardwareId} failed.", request.HardwareId);
            }
        }
    }
}
