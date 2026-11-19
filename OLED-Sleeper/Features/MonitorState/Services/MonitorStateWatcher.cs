using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.MonitorState.Commands;
using OLED_Sleeper.Features.MonitorState.Helpers;
using OLED_Sleeper.Features.MonitorState.Services.Interfaces;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.MonitorState.Services
{
    // Polls the cheap basic screen list every couple of seconds. Only when a difference has been seen on two
    // polls in a row does it do the expensive full re-scan and send a synchronisation: Windows briefly
    // reports half-finished arrangements while screens wake up or switch modes.
    public class MonitorStateWatcher : IMonitorStateWatcher
    {
        private readonly IMonitorInfoManager _screenSource;
        private readonly IMediator _commands;
        private readonly System.Timers.Timer _ticker;
        private readonly object _sync = new();
        private readonly ChangeFilter _filter = new();

        // Full (enriched) list from the last sync; becomes the "old" side of the next one.
        private IReadOnlyList<MonitorInfo> _syncedMonitors = [];

        private int _busy;              // 1 while a poll runs; overlapping ticks are dropped, not queued
        private volatile bool _halted;  // set by Stop, checked before every dispatch

        public MonitorStateWatcher(IMonitorInfoManager monitorInfoManager, IMediator mediator, double pollIntervalMs = 2000)
        {
            _screenSource = monitorInfoManager;
            _commands = mediator;
            _ticker = new System.Timers.Timer(pollIntervalMs) { AutoReset = true };
            _ticker.Elapsed += (_, _) => OnTick();
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_ticker.Enabled)
                {
                    return;
                }
            }

            _halted = false;
            _ = BeginAsync();
        }

        public void Stop()
        {
            _halted = true;
            lock (_sync)
            {
                _ticker.Stop();
            }
        }

        public void Dispose() => _ticker.Dispose();

        // First full read and sync, then polling starts.
        private async Task BeginAsync()
        {
            try
            {
                IReadOnlyList<MonitorInfo> monitors = await _screenSource.GetCurrentMonitorsAsync();
                if (_halted)
                {
                    return;
                }

                IReadOnlyList<MonitorInfo> baseline = _screenSource.GetLatestMonitorsBasicInfo();
                lock (_sync)
                {
                    _syncedMonitors = monitors;
                    _filter.Accept(baseline);
                }

                // Start-up counts as "everything just appeared": nothing before, all of these after.
                var firstSync = new SynchronizeMonitorStateCommand(oldMonitors: [], newMonitors: monitors);
                await _commands.SendAsync(firstSync);

                lock (_sync)
                {
                    if (!_halted)
                    {
                        _ticker.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not read the screens at start-up; screen changes will not be followed.");
            }
        }

        private void OnTick()
        {
            if (_halted)
            {
                return;
            }

            if (Interlocked.Exchange(ref _busy, 1) == 1)
            {
                Log.Debug("Screen poll still running; skipping this tick.");
                return;
            }

            _ = PollAsync();
        }

        private async Task PollAsync()
        {
            try
            {
                IReadOnlyList<MonitorInfo> reading = _screenSource.GetLatestMonitorsBasicInfo();

                bool confirmed;
                lock (_sync)
                {
                    confirmed = _filter.IsConfirmedChange(reading);
                }

                if (!confirmed)
                {
                    return;
                }

                // The heavy part (hardware IDs, DDC/CI) runs outside the lock.
                IReadOnlyList<MonitorInfo> rescanned = await _screenSource.RefreshMonitorsAsync();
                if (rescanned.Count == 0)
                {
                    Log.Warning("Full screen re-scan came back empty; keeping the previous screen list.");
                    return;
                }

                if (_halted)
                {
                    Log.Debug("Watcher stopped during the re-scan; not synchronising.");
                    return;
                }

                IReadOnlyList<MonitorInfo> previous;
                lock (_sync)
                {
                    previous = _syncedMonitors;
                    _syncedMonitors = rescanned;
                    _filter.Accept(reading);
                }

                await SendSyncAsync(previous, rescanned);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Checking for screen changes failed.");
            }
            finally
            {
                Volatile.Write(ref _busy, 0);
            }
        }

        private async Task SendSyncAsync(IReadOnlyList<MonitorInfo> previous, IReadOnlyList<MonitorInfo> current)
        {
            try
            {
                await _commands.SendAsync(new SynchronizeMonitorStateCommand(previous, current));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Screens changed, but synchronising to the new set failed.");
            }
        }

        // Debounces readings: a change counts only when the same new arrangement is read twice in a row.
        // Not thread-safe on its own; the watcher calls it under its lock.
        private sealed class ChangeFilter
        {
            // Always a complete basic reading, never the enriched list (which drops screens whose
            // hardware ID did not resolve and would therefore look like a permanent change).
            private IReadOnlyList<MonitorInfo> _accepted = [];
            private IReadOnlyList<MonitorInfo>? _candidate;

            public void Accept(IReadOnlyList<MonitorInfo> reading)
            {
                _accepted = reading;
                _candidate = null;
            }

            public bool IsConfirmedChange(IReadOnlyList<MonitorInfo> reading)
            {
                // An empty set is what Windows reports mid-switch; it also cancels a half-seen change.
                if (reading.Count == 0)
                {
                    Log.Debug("Screen poll saw no screens at all; ignoring it as a glitch.");
                    _candidate = null;
                    return false;
                }

                if (DisplaySetComparer.AreEquivalent(_accepted, reading))
                {
                    _candidate = null;
                    return false;
                }

                if (DisplaySetComparer.AreEquivalent(_candidate, reading))
                {
                    _candidate = null;
                    return true;
                }

                Log.Debug("Screen arrangement looks different; waiting for one more matching poll.");
                _candidate = reading;
                return false;
            }
        }
    }
}
