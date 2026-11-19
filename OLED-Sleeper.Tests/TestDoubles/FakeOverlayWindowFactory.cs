using OLED_Sleeper.Features.MonitorBlackout.Models;
using OLED_Sleeper.Features.MonitorBlackout.Services.Interfaces;

namespace OLED_Sleeper.Tests.TestDoubles
{
    // Hands out fake overlays with predictable handles (start, start+1, ...) and keeps them all for
    // inspection. Starting at zero gives overlays that never get a handle.
    public sealed class FakeOverlayWindowFactory(nint firstHandle) : IOverlayWindowFactory
    {
        private int _issued;

        public FakeOverlayWindowFactory() : this(0x4000)
        {
        }

        public List<FakeOverlayWindow> Created { get; } = [];

        public List<OverlayScene> RequestedScenes { get; } = [];

        public IOverlayWindow Create(OverlayScene scene = OverlayScene.Black)
        {
            nint handle = firstHandle == nint.Zero ? nint.Zero : firstHandle + _issued++;
            var window = new FakeOverlayWindow(handle);
            Created.Add(window);
            RequestedScenes.Add(scene);
            return window;
        }
    }
}
