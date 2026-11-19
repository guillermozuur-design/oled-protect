using Microsoft.Extensions.DependencyInjection;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.MonitorState.Commands;
using OledProtect.Features.MonitorState.Services;
using OledProtect.Features.MonitorState.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using OledProtect.Tests.TestDoubles;
using System.Windows;

namespace OledProtect.Tests.Features.MonitorState.Services
{
    // The watcher runs a real background loop with a very short interval. Negative checks wait until the
    // loop has demonstrably taken extra samples instead of sleeping for a guessed amount of time.
    public sealed class MonitorStateWatcherTests : IDisposable
    {
        private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(10);

        private static readonly List<MonitorInfo> Single = [Screen(1)];
        private static readonly List<MonitorInfo> Pair = [Screen(1), Screen(2)];
        private static readonly List<MonitorInfo> Other = [Screen(3)];

        private readonly ScriptedScreens _screens = new();
        private readonly RecordingMediator _mediator = new();
        private readonly MonitorStateWatcher _sut;

        public MonitorStateWatcherTests()
        {
            _sut = new MonitorStateWatcher(_screens, _mediator, Fast);
        }

        public void Dispose() => _sut.Dispose();

        private static MonitorInfo Screen(int n) => new()
        {
            DeviceName = @"\\.\DISPLAY" + n,
            DisplayNumber = n,
            HardwareId = "PANEL-" + n,
            Bounds = new Rect((n - 1) * 1920, 0, 1920, 1080),
            Dpi = 96,
        };

        private List<SynchronizeMonitorStateCommand> Syncs => _mediator.SentOf<SynchronizeMonitorStateCommand>();

        private static async Task Until(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for: " + what);
                await Task.Delay(5);
            }
        }

        private async Task StartAndWaitForSamples(int samples)
        {
            _sut.Start();
            await Until(() => _screens.BasicReads >= samples, $"{samples} quick readings");
        }

        [Fact]
        public async Task Start_announces_the_probed_screens_as_all_new()
        {
            _screens.Probed = Pair;
            _screens.Script(Pair);

            _sut.Start();
            await Until(() => Syncs.Count == 1, "the start-up sync");

            SynchronizeMonitorStateCommand first = Syncs[0];
            Assert.Empty(first.OldMonitors);
            Assert.Same(Pair, first.NewMonitors);
        }

        [Fact]
        public async Task A_second_start_while_running_changes_nothing()
        {
            _screens.Script(Single);

            _sut.Start();
            _sut.Start();
            await StartAndWaitForSamples(5);

            Assert.Single(Syncs);
            Assert.Equal(1, _screens.ProbeCalls);
        }

        [Fact]
        public async Task A_steady_desk_never_triggers_a_rescan()
        {
            _screens.Script(Single);

            await StartAndWaitForSamples(8);

            Assert.Equal(0, _screens.RefreshCalls);
            Assert.Single(Syncs);
        }

        [Fact]
        public async Task A_new_layout_seen_twice_is_rescanned_and_synchronised()
        {
            _screens.Probed = Single;
            _screens.Refreshed.Enqueue(Pair);
            _screens.Script(Single, Pair);

            _sut.Start();
            await Until(() => Syncs.Count == 2, "the change sync");

            Assert.Same(Single, Syncs[1].OldMonitors);
            Assert.Same(Pair, Syncs[1].NewMonitors);
            Assert.Equal(1, _screens.RefreshCalls);
        }

        [Fact]
        public async Task The_new_layout_becomes_the_reference_for_the_next_change()
        {
            _screens.Probed = Single;
            _screens.Refreshed.Enqueue(Pair);
            _screens.Refreshed.Enqueue(Other);
            _screens.Script(Single, Pair, Pair, Pair, Pair, Other);

            _sut.Start();
            await Until(() => Syncs.Count == 3, "two change syncs");

            Assert.Same(Pair, Syncs[2].OldMonitors);
            Assert.Same(Other, Syncs[2].NewMonitors);
            Assert.Equal(2, _screens.RefreshCalls);
        }

        [Theory]
        [InlineData("one odd sample, then back")]
        [InlineData("blank sample in between")]
        [InlineData("two different new layouts")]
        public async Task Unconfirmed_changes_are_ignored(string pattern)
        {
            List<MonitorInfo>[] samples = pattern switch
            {
                "one odd sample, then back" => [Single, Pair, Single],
                "blank sample in between" => [Single, Pair, [], Pair, Single],
                _ => [Single, Pair, Other, Single],
            };
            _screens.Script(samples);

            await StartAndWaitForSamples(samples.Length + 4);

            Assert.Equal(0, _screens.RefreshCalls);
            Assert.Single(Syncs);
        }

        [Fact]
        public async Task A_blank_rescan_keeps_the_old_list_and_the_change_is_confirmed_again()
        {
            _screens.Probed = Single;
            _screens.Refreshed.Enqueue([]);
            _screens.Refreshed.Enqueue(Pair);
            _screens.Script(Single, Pair);

            _sut.Start();
            await Until(() => Syncs.Count == 2, "the sync after the second rescan");

            Assert.Equal(2, _screens.RefreshCalls);
            Assert.Same(Single, Syncs[1].OldMonitors);
            Assert.Same(Pair, Syncs[1].NewMonitors);
        }

        [Fact]
        public async Task A_failing_sample_does_not_end_the_watch()
        {
            _screens.Refreshed.Enqueue(Pair);
            _screens.Script(Single, Single);
            _screens.ThrowOnRead = 2;
            _screens.Then(Pair);

            _sut.Start();
            await Until(() => Syncs.Count == 2, "a sync after the failed sample");
        }

        [Fact]
        public async Task After_stop_the_loop_takes_no_more_samples()
        {
            _screens.Script(Single);
            await StartAndWaitForSamples(3);

            _sut.Stop();
            int readsAtStop = _screens.BasicReads;
            _screens.Then(Pair);
            await Task.Delay(Fast * 10);

            Assert.InRange(_screens.BasicReads, readsAtStop, readsAtStop + 1);
            Assert.Equal(0, _screens.RefreshCalls);
            Assert.Single(Syncs);
        }

        [Fact]
        public async Task Stopping_during_the_start_up_scan_sends_nothing()
        {
            var slowScan = new TaskCompletionSource<IReadOnlyList<MonitorInfo>>();
            _screens.ProbeOverride = slowScan.Task;
            _screens.Script(Single);

            _sut.Start();
            _sut.Stop();
            slowScan.SetResult(Single);
            await Task.Delay(Fast * 10);

            Assert.Empty(_mediator.Sent);
            Assert.Equal(0, _screens.BasicReads);
        }

        [Fact]
        public async Task A_failed_start_up_does_not_poll_but_a_new_start_retries()
        {
            _screens.ProbeOverride = Task.FromException<IReadOnlyList<MonitorInfo>>(new InvalidOperationException("no displays API"));
            _screens.Script(Single);

            _sut.Start();
            await Until(() => _screens.ProbeCalls == 1, "the failing scan");
            await Task.Delay(Fast * 10);
            Assert.Empty(_mediator.Sent);
            Assert.Equal(0, _screens.BasicReads);

            _screens.ProbeOverride = null;
            _sut.Start();
            await Until(() => Syncs.Count == 1, "the retried start-up sync");
        }

        [Fact]
        public async Task A_failed_announcement_stops_the_watch_before_polling()
        {
            _mediator.Failing<SynchronizeMonitorStateCommand>(new InvalidOperationException("no handler"));
            _screens.Script(Single, Pair);

            _sut.Start();
            await Until(() => _mediator.Sent.Count == 1, "the announcement attempt");
            await Task.Delay(Fast * 10);

            Assert.Equal(1, _screens.BasicReads);
        }

        [Fact]
        public void The_container_can_build_it_with_the_default_interval()
        {
            var services = new ServiceCollection()
                .AddSingleton<IMonitorInfoManager>(new ScriptedScreens())
                .AddSingleton<IMediator>(new RecordingMediator())
                .AddSingleton<IMonitorStateWatcher, MonitorStateWatcher>();

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.IsType<MonitorStateWatcher>(provider.GetRequiredService<IMonitorStateWatcher>());
        }

        // Quick readings follow a script; when it runs out the last entry repeats. Probed scans come from a queue.
        private sealed class ScriptedScreens : IMonitorInfoManager
        {
            private readonly object _lock = new();
            private readonly Queue<List<MonitorInfo>> _script = new();
            private List<MonitorInfo> _last = [];
            private int _basicReads;
            private int _probeCalls;
            private int _refreshCalls;

            public IReadOnlyList<MonitorInfo> Probed { get; set; } = [];

            public Task<IReadOnlyList<MonitorInfo>>? ProbeOverride { get; set; }

            public Queue<IReadOnlyList<MonitorInfo>> Refreshed { get; } = new();

            // 1-based number of the quick reading that throws, or 0 for none.
            public int ThrowOnRead { get; set; }

            public int BasicReads => Volatile.Read(ref _basicReads);

            public int ProbeCalls => Volatile.Read(ref _probeCalls);

            public int RefreshCalls => Volatile.Read(ref _refreshCalls);

            public void Script(params List<MonitorInfo>[] readings)
            {
                lock (_lock)
                {
                    foreach (List<MonitorInfo> reading in readings)
                    {
                        _script.Enqueue(reading);
                    }
                }
            }

            public void Then(List<MonitorInfo> reading) => Script(reading);

            public Task<IReadOnlyList<MonitorInfo>> GetCurrentMonitorsAsync()
            {
                Interlocked.Increment(ref _probeCalls);
                return ProbeOverride ?? Task.FromResult(Probed);
            }

            public Task<IReadOnlyList<MonitorInfo>> RefreshMonitorsAsync()
            {
                Interlocked.Increment(ref _refreshCalls);
                lock (_lock)
                {
                    return Task.FromResult(Refreshed.Count > 0 ? Refreshed.Dequeue() : Probed);
                }
            }

            public List<MonitorInfo> GetLatestMonitorsBasicInfo()
            {
                int number = Interlocked.Increment(ref _basicReads);
                lock (_lock)
                {
                    if (_script.Count > 0)
                    {
                        _last = _script.Dequeue();
                    }

                    if (number == ThrowOnRead)
                    {
                        throw new InvalidOperationException("display query failed");
                    }

                    return _last;
                }
            }

            public void EnrichMonitorInfoList(List<MonitorInfo> readings)
            {
            }
        }
    }
}
