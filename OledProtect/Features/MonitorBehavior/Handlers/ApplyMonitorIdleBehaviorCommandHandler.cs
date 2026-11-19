using OledProtect.Features.MonitorBehavior.Commands;
using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorBlackout.Commands;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorDimming.Commands;
using OledProtect.Features.UserSettings.Models;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorBehavior.Handlers
{
    // Turns "this screen is idle" into the concrete command for the behaviour the user picked. Failures of
    // that command are left to the caller.
    public class ApplyMonitorIdleBehaviorCommandHandler(IMediator mediator) : ICommandHandler<ApplyMonitorIdleBehaviorCommand>
    {
        public Task HandleAsync(ApplyMonitorIdleBehaviorCommand command)
        {
            MonitorSettings settings = command.EventArgs.Settings;
            string id = command.EventArgs.HardwareId;

            return settings.Behavior switch
            {
                MonitorBehaviorType.Dim => mediator.SendAsync(new ApplyDimCommand { HardwareId = id, DimLevel = (int)settings.DimLevel }),
                MonitorBehaviorType.Blackout => Cover(id, OverlayScene.Black, settings.LowerBrightnessOnBlackout),
                MonitorBehaviorType.Screensaver => Cover(id, OverlayScene.Stars, lowerBrightness: false),
                MonitorBehaviorType.Flights => Cover(id, OverlayScene.Flights, lowerBrightness: false),
                _ => LeaveAlone(id),
            };
        }

        // Screensaver scenes need the panel lit, so only plain black may also turn the backlight down.
        private Task Cover(string id, OverlayScene scene, bool lowerBrightness) =>
            mediator.SendAsync(new ApplyBlackoutOverlayCommand { HardwareId = id, Scene = scene, LowerBrightness = lowerBrightness });

        private static Task LeaveAlone(string id)
        {
            Log.Information("Screen {HardwareId} is idle but has no idle behaviour set.", id);
            return Task.CompletedTask;
        }
    }
}
