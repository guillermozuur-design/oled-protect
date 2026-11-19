using OLED_Sleeper.Features.MonitorIdleDetection.Models;
using OLED_Sleeper.Messaging.Interfaces;

namespace OLED_Sleeper.Features.MonitorBehavior.Commands
{
    // Sent by idle detection when a screen's idle timer runs out; the settings inside the event args
    // decide what actually happens to it.
    public sealed class ApplyMonitorIdleBehaviorCommand : ICommand
    {
        public ApplyMonitorIdleBehaviorCommand(MonitorIdleStateEventArgs eventArgs)
        {
            EventArgs = eventArgs;
        }

        public MonitorIdleStateEventArgs EventArgs { get; init; }
    }
}
