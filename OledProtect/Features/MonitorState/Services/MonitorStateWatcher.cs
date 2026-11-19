using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.MonitorState.Commands;
using OledProtect.Features.MonitorState.Helpers;
using OledProtect.Features.MonitorState.Services.Interfaces;
using OledProtect.Messaging.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorState.Services
{
    // One background loop per Start: announce the screens once, then sample the quick (unprobed) reading on a
    // PeriodicTimer. A new arrangement has to be sampled twice in a row before the expensive probed scan runs
    // and a SynchronizeMonitorStateCommand goes out, because Windows shows in-between layouts while panels
    // wake up or change mode. All loop state lives in the loop itself, so no locking is needed for it.
    public sealed class MonitorStateWatcher : IMonitorStateWatcher
    {
        private const int SamplesToConfirm = 2;
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

        private readonly IMonitorInfoManager _monitors;
        private readonly IMediator _mediator;
        private readonly TimeSpan _interval;
        private readonly object _gate = new();

        // Non-null while a loop is alive; cancelling it is how Stop ends that loop.
        private CancellationTokenSource? _loop;

        public MonitorStateWatcher(IMonitorInfoManager monitorInfoManager, IMediator mediator, TimeSpan? pollInterval = null)
        {
            _monitors = monitorInfoManager;
            _mediator = mediator;
            _interval = pollInterval ?? DefaultInterval;
        }

        public void Start()
        {
            CancellationTokenSource loop;
            lock (_gate)
            {
                if (_loop is not null)
                {
                    return;
                }

                loop = _loop = new CancellationTokenSource();
            }

            _ = RunAsync(loop);
        }

        public void Stop()
        {
            lock (_gate)
            {
                _loop?.Cancel();
                _loop = null;
            }
        }

        public void Dispose() => Stop();

        private async Task RunAsync(CancellationTokenSource loop)
        {
            CancellationToken stopped = loop.Token;
            try
            {
                await WatchAsync(stopped);
            }
            catch (OperationCanceledException) when (stopped.IsCancellationRequested)
            {
                // Normal end after Stop.
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Display watcher gave up: the initial screen scan or announcement failed.");
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_loop, loop))
                    {
                        _loop = null;
                    }
                }

                loop.Dispose();
            }
        }

        private async Task WatchAsync(CancellationToken stopped)
        {
            IReadOnlyList<MonitorInfo> announced = await _monitors.GetCurrentMonitorsAsync();
            if (stopped.IsCancellationRequested)
            {
                return;
            }

            // Compare quick readings with quick readings only: the probed list leaves out screens without a
            // hardware ID and would never equal a quick reading.
            IReadOnlyList<MonitorInfo> knownLayout = _monitors.GetLatestMonitorsBasicInfo();

            // At start-up every screen is new.
            await _mediator.SendAsync(new SynchronizeMonitorStateCommand([], announced));

            IReadOnlyList<MonitorInfo>? suspect = null;
            int sightings = 0;

            // PeriodicTimer never overlaps: a slow round simply swallows the ticks it missed.
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stopped))
            {
                try
                {
                    IReadOnlyList<MonitorInfo> sample = _monitors.GetLatestMonitorsBasicInfo();

                    if (sample.Count == 0)
                    {
                        Log.Debug("Display watcher: blank sample (display switch in progress?), skipped.");
                        sightings = 0;
                        continue;
                    }

                    if (DisplaySetComparer.AreEquivalent(knownLayout, sample))
                    {
                        sightings = 0;
                        continue;
                    }

                    sightings = sightings > 0 && DisplaySetComparer.AreEquivalent(suspect, sample) ? sightings + 1 : 1;
                    suspect = sample;
                    if (sightings < SamplesToConfirm)
                    {
                        Log.Debug("Display watcher: layout differs, waiting for sample {Seen} of {Needed}.", sightings + 1, SamplesToConfirm);
                        continue;
                    }

                    sightings = 0;
                    IReadOnlyList<MonitorInfo> probed = await _monitors.RefreshMonitorsAsync();
                    if (probed.Count == 0)
                    {
                        Log.Warning("Display watcher: probed scan found no screens; previous list kept.");
                        continue;
                    }

                    if (stopped.IsCancellationRequested)
                    {
                        return;
                    }

                    IReadOnlyList<MonitorInfo> previous = announced;
                    announced = probed;
                    knownLayout = sample;
                    await _mediator.SendAsync(new SynchronizeMonitorStateCommand(previous, probed));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Display watcher: handling a layout sample failed.");
                }
            }
        }
    }
}
