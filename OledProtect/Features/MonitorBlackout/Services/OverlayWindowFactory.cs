using OledProtect.Features.Flights;
using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Features.Sky;
using System.Diagnostics.CodeAnalysis;

namespace OledProtect.Features.MonitorBlackout.Services
{
    // Builds real overlays, handing each the shared sky and flight settings.
    [ExcludeFromCodeCoverage]
    public class OverlayWindowFactory(PanoramaSettings panorama, FlightsSettings flights) : IOverlayWindowFactory
    {
        public IOverlayWindow Create(OverlayScene scene = OverlayScene.Black) => new OverlayWindow(scene, panorama, flights);
    }
}
