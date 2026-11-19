using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.MonitorIdleDetection.Models;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorBehavior.Handlers
{
    public class ApplyMonitorActiveBehaviorCommandHandler(IMonitorBlackoutService blackout, IMediator mediator)
        : ICommandHandler<ApplyMonitorActiveBehaviorCommand>
    {
        public async Task HandleAsync(ApplyMonitorActiveBehaviorCommand command)
        {
            var wake = command.EventArgs;

            // Our own overlay becoming the foreground window is not the user coming back. The flag is set
            // before the first await, so the sender (which does not await) still sees it in time.
            if (IsCausedByOwnOverlay(wake))
            {
                Log.Debug("Screen #{DisplayNumber}: focus moved to our own overlay; not waking it.", wake.DisplayNumber);
                wake.IsIgnored = true;
                return;
            }

            Log.Information("Screen #{DisplayNumber} sees activity again ({Reason}); restoring it.", wake.DisplayNumber, wake.Reason);
            try
            {
                await mediator.SendAsync(new RestoreMonitorStateCommand { HardwareId = wake.HardwareId });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Restoring screen #{DisplayNumber} failed.", wake.DisplayNumber);
            }
        }

        private bool IsCausedByOwnOverlay(MonitorIdleStateEventArgs wake) =>
            wake.Reason == ActivityReason.ActiveWindow && blackout.IsOverlayWindow(wake.ForegroundWindowHandle);
    }
}
