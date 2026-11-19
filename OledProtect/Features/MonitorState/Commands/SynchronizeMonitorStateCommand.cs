using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorState.Commands
{
    // Raised whenever the set of screens changes (and once at start-up with an empty "before" list), so
    // overlays, brightness and idle tracking can be rebuilt for the new arrangement.
    public sealed class SynchronizeMonitorStateCommand(IReadOnlyList<MonitorInfo> oldMonitors, IReadOnlyList<MonitorInfo> newMonitors)
        : ICommand
    {
        public IReadOnlyList<MonitorInfo> OldMonitors => oldMonitors;

        public IReadOnlyList<MonitorInfo> NewMonitors => newMonitors;
    }
}
