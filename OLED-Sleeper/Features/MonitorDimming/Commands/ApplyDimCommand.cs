using OLED_Sleeper.Messaging.Interfaces;

namespace OLED_Sleeper.Features.MonitorDimming.Commands
{
    /// <summary>
    /// Lowers one monitor's hardware brightness, remembering the level it had so it can be put back.
    /// </summary>
    public sealed class ApplyDimCommand : ICommand
    {
        /// <summary>Monitor to dim.</summary>
        public required string HardwareId { get; init; }

        /// <summary>Target level in percent (0-100); the service maps it onto the panel's own range.</summary>
        public int DimLevel { get; init; }
    }
}
