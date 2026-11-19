using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorDimming.Commands
{
    /// <summary>
    /// Puts a dimmed monitor back on the brightness it had before it was dimmed.
    /// </summary>
    public sealed class ApplyUndimCommand : ICommand
    {
        /// <summary>Monitor to restore.</summary>
        public required string HardwareId { get; init; }
    }
}
