using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorBehavior.Handlers
{
    // Undoes every idle behaviour at once. Both halves are harmless when there is nothing to undo, so no
    // bookkeeping of what was applied is needed.
    public class RestoreMonitorStateCommandHandler(IMediator mediator) : ICommandHandler<RestoreMonitorStateCommand>
    {
        public Task HandleAsync(RestoreMonitorStateCommand command)
        {
            string id = command.HardwareId;
            Log.Information("Waking screen {HardwareId}: removing overlay and restoring brightness.", id);

            // Run side by side, and one failing must never keep the other from happening.
            return Task.WhenAll(
                SendLogged(new HideBlackoutOverlayCommand { HardwareId = id }, "remove the overlay", id),
                SendLogged(new ApplyUndimCommand { HardwareId = id }, "restore the brightness", id));
        }

        private async Task SendLogged<TCommand>(TCommand step, string what, string id) where TCommand : ICommand
        {
            try
            {
                await mediator.SendAsync(step);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not {What} of screen {HardwareId}.", what, id);
            }
        }
    }
}
