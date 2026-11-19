using OLED_Sleeper.Features.Flights;
using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;
using OLED_Sleeper.Features.Sky;
using System.Diagnostics.CodeAnalysis;

namespace OLED_Sleeper.Features.MonitorBlackout.Services
{
    // Builds real overlays, handing each the shared sky and flight settings.
    [ExcludeFromCodeCoverage]
    public class OverlayWindowFactory(PanoramaSettings panorama, FlightsSettings flights) : IOverlayWindowFactory
    {
        public IOverlayWindow Create(OverlayScene scene = OverlayScene.Black) => new OverlayWindow(scene, panorama, flights);
    }
}
