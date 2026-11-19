using OledProtect.Features.MonitorBlackout.Models;
using System.Windows;

namespace OledProtect.Features.MonitorBlackout.Services.Interfaces
{
    // Owns the overlays: at most one per screen, keyed by hardware ID. Callable from any thread.
    public interface IMonitorBlackoutService
    {
        // Does nothing when the screen already has an overlay. Bounds are physical pixels.
        Task ShowBlackoutOverlayAsync(string hardwareId, Rect bounds, OverlayScene scene = OverlayScene.Black);

        // Does nothing when the screen has no overlay.
        Task HideBlackoutOverlayAsync(string hardwareId);

        // Lets idle detection recognise focus landing on one of our own overlays.
        bool IsOverlayWindow(nint windowHandle);
    }
}
