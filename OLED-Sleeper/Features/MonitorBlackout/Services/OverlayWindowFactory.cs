using OLED_Sleeper.Features.MonitorBlackout.Models;
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
        private readonly OLED_Sleeper.Features.Flights.FlightsSettings _flights;

        public OverlayWindowFactory(OLED_Sleeper.Features.Sky.PanoramaSettings panorama, OLED_Sleeper.Features.Flights.FlightsSettings flights)
        {
            _panorama = panorama;
            _flights = flights;
        }

        public IOverlayWindow Create(OverlayScene scene = OverlayScene.Black) => new OverlayWindow(scene, _panorama, _flights);
    }
}
