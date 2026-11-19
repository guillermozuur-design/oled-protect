using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using System.Windows;

namespace OledProtect.Tests.TestDoubles
{
    // Stands in for a real overlay: remembers what happened to it and, like the real one, only has a
    // handle between Show and Close.
    public sealed class FakeOverlayWindow(nint handleOnceShown) : IOverlayWindow
    {
        private Rect? _shownAt;

        public Rect? ShownBounds => _shownAt;

        public int CloseCalls { get; private set; }

        public bool IsClosed => CloseCalls > 0;

        public nint Handle => _shownAt.HasValue && !IsClosed ? handleOnceShown : nint.Zero;

        public void Show(Rect bounds) => _shownAt = bounds;

        public void Close() => CloseCalls++;
    }
}
