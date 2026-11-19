using OledProtect.Features.MonitorIdleDetection.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorBehavior.Commands
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
