using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorDimming.Handlers
{
    /// <summary>
    /// Hands a dim request to the dimming service. A failure is logged, not thrown: the idle loop that sends
    /// this request must keep running when one panel misbehaves.
    /// </summary>
    public sealed class ApplyDimCommandHandler(IMonitorDimmingService dimming) : ICommandHandler<ApplyDimCommand>
    {
        public async Task HandleAsync(ApplyDimCommand request)
        {
            Log.Information("Dimming monitor {HardwareId} to {DimLevel}%.", request.HardwareId, request.DimLevel);
            try
            {
                await dimming.DimMonitorAsync(request.HardwareId, request.DimLevel);
            }
            catch (Exception error)
            {
                Log.Error(error, "Dimming monitor {HardwareId} to {DimLevel}% failed.", request.HardwareId, request.DimLevel);
            }
        }
    }
}
