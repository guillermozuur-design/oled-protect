using OLED_Sleeper.Messaging.Interfaces;

namespace OLED_Sleeper.Features.MonitorDimming.Commands
{
    /// <summary>
    /// Sent at startup: undims every monitor the state file still lists as dimmed, for instance after a crash.
    /// </summary>
    public sealed class RestoreBrightnessOnAllMonitorsCommand : ICommand;
}
