using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorDimming.Commands
{
    /// <summary>
    /// Sent at startup: undims every monitor the state file still lists as dimmed, for instance after a crash.
    /// </summary>
    public sealed class RestoreBrightnessOnAllMonitorsCommand : ICommand;
}
