using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;

namespace OledProtect.Tests.TestDoubles
{
    // Builds fake overlays and keeps every one of them. Handles are 0x7100, 0x7101, ... unless the factory
    // is told to build windows that never receive a handle.
    public sealed class FakeOverlayWindowFactory(bool withHandles = true) : IOverlayWindowFactory
    {
        private const int HandleBase = 0x7100;

        private readonly List<FakeOverlayWindow> _built = [];

        public IReadOnlyList<FakeOverlayWindow> Built => _built;

        public IOverlayWindow Create(OverlayScene scene = OverlayScene.Black)
        {
            nint handle = withHandles ? HandleBase + _built.Count : 0;
            var window = new FakeOverlayWindow(handle, scene);
            _built.Add(window);
            return window;
        }
    }
}
