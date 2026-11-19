using OledProtect.Features.MonitorInformation.Models;

namespace OledProtect.Features.MonitorState.Models
{
    // The screen set before and after a change, for anyone who prefers an event over the command.
    public sealed class MonitorsChangedEventArgs(IReadOnlyList<MonitorInfo> oldMonitors, IReadOnlyList<MonitorInfo> newMonitors)
        : EventArgs
    {
        public IReadOnlyList<MonitorInfo> OldMonitors => oldMonitors;

        public IReadOnlyList<MonitorInfo> NewMonitors => newMonitors;
    }
}
