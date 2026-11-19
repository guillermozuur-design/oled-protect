using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using OledProtect.Infrastructure.Runtime.Interfaces;
using System.Windows;

namespace OledProtect.Features.MonitorBlackout.Services
{
    // Keeps track of which screen is covered by which overlay. Windows are created and closed on the UI
    // thread; IsOverlayWindow is asked from the idle-detection thread, hence the lock around the table.
    public class MonitorBlackoutService(IDispatcher dispatcher, IOverlayWindowFactory overlayWindowFactory) : IMonitorBlackoutService
    {
        // The handle is captured right after Show: a closed overlay reports zero, so asking the window
        // again later would no longer tell us which handle it used to have.
        private sealed record Cover(IOverlayWindow Window, nint ShownHandle);

        private readonly Dictionary<string, Cover> _coversByScreen = new();
        private readonly object _gate = new();

        // Both are async so that even a dispatcher that refuses work (app shutting down) fails through the
        // task; several callers do not await.
        public async Task ShowBlackoutOverlayAsync(string hardwareId, Rect bounds, OverlayScene scene = OverlayScene.Black) =>
            await dispatcher.InvokeAsync(() => PutUp(hardwareId, bounds, scene));

        public async Task HideBlackoutOverlayAsync(string hardwareId) =>
            await dispatcher.InvokeAsync(() => TakeDown(hardwareId));

        public bool IsOverlayWindow(nint windowHandle)
        {
            // Zero is what a missing or closed window reports; it must never match.
            if (windowHandle == nint.Zero)
            {
                return false;
            }

            lock (_gate)
            {
                return _coversByScreen.Values.Any(cover => cover.ShownHandle == windowHandle);
            }
        }

        private void PutUp(string hardwareId, Rect bounds, OverlayScene scene)
        {
            if (IsCovered(hardwareId))
            {
                return;
            }

            // Window work stays outside the lock so the idle thread is never held up by it.
            IOverlayWindow window = overlayWindowFactory.Create(scene);
            window.Show(bounds);
            var cover = new Cover(window, window.Handle);

            lock (_gate)
            {
                _coversByScreen[hardwareId] = cover;
            }
        }

        private void TakeDown(string hardwareId)
        {
            Cover? cover;
            lock (_gate)
            {
                if (!_coversByScreen.Remove(hardwareId, out cover))
                {
                    return;
                }
            }

            cover.Window.Close();
        }

        private bool IsCovered(string hardwareId)
        {
            lock (_gate)
            {
                return _coversByScreen.ContainsKey(hardwareId);
            }
        }
    }
}
