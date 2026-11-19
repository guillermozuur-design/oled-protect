using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorBehavior.Commands
{
    // Brings one screen fully back: overlay gone and brightness back where it was. Safe to send for a
    // screen that is not asleep at all.
    public sealed class RestoreMonitorStateCommand : ICommand
    {
        private readonly string _hardwareId = string.Empty;

        public required string HardwareId
        {
            get => _hardwareId;
            init => _hardwareId = value ?? throw new ArgumentNullException(nameof(HardwareId));
        }
    }
}
