using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Messaging.Interfaces;

namespace OLED_Sleeper.Features.MonitorBlackout.Commands
{
    // Covers one screen with an overlay. Optionally also drops its backlight to zero, which only matters
    // for plain black: a scene drawn on top would become invisible.
    public sealed class ApplyBlackoutOverlayCommand : ICommand
    {
        private readonly string _hardwareId = string.Empty;

        public required string HardwareId
        {
            get => _hardwareId;
            init => _hardwareId = value ?? throw new ArgumentNullException(nameof(HardwareId));
        }

        public bool LowerBrightness { get; init; }

        public OverlayScene Scene { get; init; } = OverlayScene.Black;
    }
}
