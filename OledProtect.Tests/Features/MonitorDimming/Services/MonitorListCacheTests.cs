using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services;
using OledProtect.Features.MonitorInformation.Services.Interfaces;

namespace OledProtect.Tests.Features.MonitorDimming.Services
{
    // The dimming service finds its monitors through MonitorInfoManager's cached list; these tests pin how that
    // list is probed, shared, refreshed and recovered after a failure.
    public sealed class MonitorListCacheTests
    {
        private readonly ScriptedProvider _windows = new();
        private readonly MonitorInfoManager _cache;

        public MonitorListCacheTests() => _cache = new MonitorInfoManager(_windows);

        [Fact]
        public async Task First_request_probes_and_later_requests_reuse_the_result()
        {
            var first = await _cache.GetCurrentMonitorsAsync();
            var again = await _cache.GetCurrentMonitorsAsync();

            Assert.Same(first, again);
            Assert.Equal(1, _windows.Listings);
            Assert.Equal(["GSM-1"], first.Select(m => m.HardwareId));
            Assert.Equal(new DdcCiCapabilities(true, 255), first[0].Capabilities);
        }

        [Fact]
        public async Task Requests_that_arrive_during_the_probe_share_it()
        {
            _windows.Hold();
            var requests = Enumerable.Range(0, 4).Select(_ => _cache.GetCurrentMonitorsAsync()).ToList();
            await _windows.Held;

            _windows.Resume();
            var lists = await Task.WhenAll(requests);

            Assert.Equal(1, _windows.Listings);
            Assert.All(lists, list => Assert.Same(lists[0], list));
        }

        [Fact]
        public async Task Failed_probe_is_not_cached()
        {
            _windows.FailNext = true;

            await Assert.ThrowsAsync<InvalidOperationException>(_cache.GetCurrentMonitorsAsync);
            var retried = await _cache.GetCurrentMonitorsAsync();

            Assert.Single(retried);
            Assert.Equal(2, _windows.Listings);
        }

        [Fact]
        public async Task Refresh_probes_again_and_becomes_the_cached_list()
        {
            var before = await _cache.GetCurrentMonitorsAsync();
            _windows.Ids = ["GSM-1", "DEL-2"];

            var refreshed = await _cache.RefreshMonitorsAsync();
            var after = await _cache.GetCurrentMonitorsAsync();

            Assert.Single(before);
            Assert.Equal(2, refreshed.Count);
            Assert.Same(refreshed, after);
            Assert.Equal(2, _windows.Listings);
        }

        [Fact]
        public async Task Request_made_after_a_refresh_started_gets_the_refreshed_list()
        {
            await _cache.GetCurrentMonitorsAsync();
            _windows.Ids = ["DEL-2"];
            _windows.Hold();

            var refresh = _cache.RefreshMonitorsAsync();
            await _windows.Held;
            var request = _cache.GetCurrentMonitorsAsync();
            _windows.Resume();

            Assert.Equal(["DEL-2"], (await request).Select(m => m.HardwareId));
            Assert.Same(await refresh, await request);
        }

        [Fact]
        public async Task Refresh_requested_while_another_runs_joins_it()
        {
            _windows.Hold();
            var running = _cache.RefreshMonitorsAsync();
            await _windows.Held;

            var joining = _cache.RefreshMonitorsAsync();
            _windows.Resume();

            Assert.Same(await running, await joining);
            Assert.Equal(1, _windows.Listings);
        }

        [Fact]
        public async Task Refresh_after_a_finished_one_probes_anew()
        {
            await _cache.RefreshMonitorsAsync();
            await _cache.RefreshMonitorsAsync();

            Assert.Equal(2, _windows.Listings);
        }

        [Fact]
        public void Quick_reading_comes_straight_from_windows_without_probing()
        {
            _windows.Ids = ["GSM-1", ""];

            var quick = _cache.GetLatestMonitorsBasicInfo();

            Assert.Equal(2, quick.Count);
            Assert.All(quick, m => Assert.Null(m.Capabilities));
            Assert.Equal(0, _windows.Probes);
        }

        [Fact]
        public void Enriching_drops_nameless_monitors_and_probes_the_rest()
        {
            var readings = new List<MonitorInfo>
            {
                new() { HardwareId = "GSM-1", DeviceName = @"\\.\DISPLAY1" },
                new() { HardwareId = "", DeviceName = @"\\.\DISPLAY2" },
                new() { HardwareId = "DEL-2", DeviceName = @"\\.\DISPLAY3" },
            };

            _cache.EnrichMonitorInfoList(readings);

            Assert.Equal(["GSM-1", "DEL-2"], readings.Select(m => m.HardwareId));
            Assert.All(readings, m => Assert.NotNull(m.Capabilities));
            Assert.Equal(2, _windows.Probes);
        }

        [Fact]
        public void Enriching_nothing_is_harmless()
        {
            _cache.EnrichMonitorInfoList(null!);

            Assert.Equal(0, _windows.Probes);
        }

        // Lists one monitor per entry in Ids; can be paused inside a listing or made to fail once.
        private sealed class ScriptedProvider : IMonitorInfoProvider
        {
            private readonly ManualResetEventSlim _open = new(true);
            private TaskCompletionSource _held = new();
            private int _listings;
            private int _probes;

            public string[] Ids { get; set; } = ["GSM-1"];

            public bool FailNext { get; set; }

            public int Listings => Volatile.Read(ref _listings);

            public int Probes => Volatile.Read(ref _probes);

            public Task Held => _held.Task;

            public void Hold()
            {
                _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _open.Reset();
            }

            public void Resume() => _open.Set();

            public List<MonitorInfo> GetAllMonitorsBasicInfo()
            {
                Interlocked.Increment(ref _listings);
                _held.TrySetResult();
                _open.Wait();

                if (FailNext)
                {
                    FailNext = false;
                    throw new InvalidOperationException("display enumeration failed");
                }

                return Ids.Select((id, i) => new MonitorInfo { HardwareId = id, DeviceName = $@"\\.\DISPLAY{i + 1}" }).ToList();
            }

            public DdcCiCapabilities GetDdcCiCapabilities(MonitorInfo target)
            {
                Interlocked.Increment(ref _probes);
                return new DdcCiCapabilities(true, 255);
            }
        }
    }
}
