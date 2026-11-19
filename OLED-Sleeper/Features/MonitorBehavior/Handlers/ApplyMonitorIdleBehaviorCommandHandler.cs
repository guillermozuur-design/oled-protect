using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.Features.MonitorBlackout.Commands;
using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorDimming.Commands;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.MonitorBehavior.Handlers
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
