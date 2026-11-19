using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using System.Windows;

namespace OledProtect.Tests.TestDoubles
{
    // Overlay without a real window. Goes Created -> Visible -> Closed and, like a real window, only reports
    // its handle while it is visible.
    public sealed class FakeOverlayWindow(nint handle, OverlayScene scene) : IOverlayWindow
    {
        public enum Phase
        {
            Created,
            Visible,
            Closed,
        }

        public OverlayScene Scene { get; } = scene;

        public Phase State { get; private set; } = Phase.Created;

        public Rect? Area { get; private set; }

        public int Closes { get; private set; }

        // The handle it gets while visible, readable in any phase.
        public nint AssignedHandle { get; } = handle;

        public nint Handle => State == Phase.Visible ? AssignedHandle : 0;

        public void Show(Rect bounds)
        {
            Area = bounds;
            if (State == Phase.Created)
            {
                State = Phase.Visible;
            }
        }

        public void Close()
        {
            Closes++;
            State = Phase.Closed;
        }
    }
}
