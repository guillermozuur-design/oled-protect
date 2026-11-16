using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using System.Diagnostics.CodeAnalysis;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    /// <summary>
    /// Creates <see cref="OverlayWindow"/> instances.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class OverlayWindowFactory : IOverlayWindowFactory
    {
        /// <inheritdoc />
        private readonly OLED_Sleeper.Features.Sky.PanoramaSettings _panorama;

        public OverlayWindowFactory(OLED_Sleeper.Features.Sky.PanoramaSettings panorama)
        {
            _panorama = panorama;
        }

        public IOverlayWindow Create(bool screensaver = false) => new OverlayWindow(screensaver, _panorama);
    }
}
