using OledProtect.Messaging.Interfaces;

namespace OledProtect.Features.MonitorBlackout.Commands
{
    // Removes the overlay from one screen, if it has one.
    public sealed class HideBlackoutOverlayCommand : ICommand
    {
        private readonly string _hardwareId = string.Empty;

        public required string HardwareId
        {
            get => _hardwareId;
            init => _hardwareId = value ?? throw new ArgumentNullException(nameof(HardwareId));
        }
    }
}
