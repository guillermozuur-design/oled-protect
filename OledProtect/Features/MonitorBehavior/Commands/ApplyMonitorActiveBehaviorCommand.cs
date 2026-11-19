using OledProtect.Features.MonitorIdleDetection.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorBehavior.Commands
{
    // Sent by idle detection when an idle screen sees activity again. The handler may set
    // EventArgs.IsIgnored to tell the sender the wake-up was not real.
    public sealed class ApplyMonitorActiveBehaviorCommand : ICommand
    {
        public ApplyMonitorActiveBehaviorCommand(MonitorIdleStateEventArgs eventArgs)
        {
            EventArgs = eventArgs;
        }

        public MonitorIdleStateEventArgs EventArgs { get; init; }
    }
}
