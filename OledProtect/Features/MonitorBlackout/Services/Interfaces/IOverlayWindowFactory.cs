using OledProtect.Features.MonitorBlackout.Models;

namespace OledProtect.Features.MonitorBlackout.Services.Interfaces
{
    // Seam so the blackout service can be tested without real windows. UI thread only.
    public interface IOverlayWindowFactory
    {
        // Returns an overlay that is not on screen yet.
        IOverlayWindow Create(OverlayScene scene = OverlayScene.Black);
    }
}
