using OledProtect.Features.MonitorBlackout.Models;
using OledProtect.Features.MonitorBlackout.Services.Interfaces;
using System.Windows;

namespace OledProtect.Tests.TestDoubles
{
    // Blackout service that only keeps a list of requests. Handles added to OwnHandles count as our overlays.
    public sealed class RecordingBlackoutService : IMonitorBlackoutService
    {
        public sealed record Request(string Kind, string HardwareId, Rect? Area = null, OverlayScene? Scene = null);

        public List<Request> Requests { get; } = [];

        public HashSet<nint> OwnHandles { get; } = [];

        public Exception? FailWith { get; set; }

        public Task ShowBlackoutOverlayAsync(string hardwareId, Rect bounds, OverlayScene scene = OverlayScene.Black) =>
            Record(new Request("show", hardwareId, bounds, scene));

        public Task HideBlackoutOverlayAsync(string hardwareId) => Record(new Request("hide", hardwareId));

        public bool IsOverlayWindow(nint windowHandle) => OwnHandles.Contains(windowHandle);

        private Task Record(Request request)
        {
            Requests.Add(request);
            return FailWith is null ? Task.CompletedTask : Task.FromException(FailWith);
        }
    }
}
