using OledProtect.Features.MonitorDimming.Services.Interfaces;
using System.Collections.Concurrent;

namespace OledProtect.Tests.TestDoubles
{
    /// <summary>
    /// A simulated DDC/CI bus. Device names wired with <see cref="Wire"/> lead to a <see cref="FakeDdcCiSession"/>;
    /// any other name is unreachable. <see cref="Stall"/> can freeze one open call so a test can look at what
    /// happens around an operation that is still running.
    /// </summary>
    public sealed class FakeDdcCiAccess : IDdcCiAccess
    {
        private readonly ConcurrentDictionary<string, FakeDdcCiSession> _panels = new();
        private readonly ConcurrentQueue<string> _dialled = new();
        private Stall? _stall;

        /// <summary>Every device name an open was attempted for, reachable or not, in order.</summary>
        public IReadOnlyList<string> Dialled => [.. _dialled];

        /// <summary>Connects <paramref name="deviceName"/> to a new simulated panel that starts at <paramref name="level"/>.</summary>
        public FakeDdcCiSession Wire(string deviceName, uint level) => _panels[deviceName] = new FakeDdcCiSession(level);

        /// <summary>Freezes the next open of <paramref name="deviceName"/> until the returned stall is released.</summary>
        public Stall StallNextOpen(string deviceName) => _stall = new Stall(deviceName);

        public IDdcCiSession? OpenSession(string gdiDeviceName)
        {
            _dialled.Enqueue(gdiDeviceName);

            var stall = _stall;
            if (stall?.DeviceName == gdiDeviceName && Interlocked.CompareExchange(ref _stall, null, stall) == stall)
            {
                stall.Hold();
            }

            return _panels.GetValueOrDefault(gdiDeviceName);
        }

        /// <summary>A frozen open call. Disposing it releases the call as well.</summary>
        public sealed class Stall(string deviceName) : IDisposable
        {
            private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ManualResetEventSlim _released = new();

            internal string DeviceName { get; } = deviceName;

            /// <summary>Completes once the frozen call has arrived.</summary>
            public Task Reached => _reached.Task;

            public void Release() => _released.Set();

            public void Dispose() => Release();

            internal void Hold()
            {
                _reached.TrySetResult();
                _released.Wait();
            }
        }
    }
}
