using OLED_Sleeper.Features.MonitorBlackout.Models;

namespace OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces
{
    // Seam so the blackout service can be tested without real windows. UI thread only.
    public interface IOverlayWindowFactory
    {
        // Returns an overlay that is not on screen yet.
        IOverlayWindow Create(OverlayScene scene = OverlayScene.Black);
    }
}
