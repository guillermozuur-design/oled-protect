using OledProtect.Features.MonitorDimming.Helpers;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using Serilog;
using System.Collections.Concurrent;

namespace OledProtect.Features.MonitorDimming.Services
{
    /// <summary>
    /// Dims, restores and schedules monitor brightness over DDC/CI. Operations on one monitor are serialised;
    /// different monitors proceed in parallel. Safe to call from any thread.
    /// </summary>
    public sealed class MonitorDimmingService : IMonitorDimmingService
    {
        // A restore is only trusted once the panel reads back the value; a panel that is still waking up may need
        // a few tries before it takes the write.
        private const int MaxRestoreTries = 3;
        private static readonly TimeSpan RestoreRetryPause = TimeSpan.FromMilliseconds(200);

        // Panels quantise their brightness steps, so a read-back this close to the written value counts as applied.
        private const uint ReadBackTolerance = 2;

        private readonly IMonitorInfoManager _monitors;
        private readonly IDdcCiAccess _bus;
        private readonly IOriginalBrightnessStore _originals;
        private readonly BrightnessFadeOptions _fade;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _monitorLocks = new();

        public MonitorDimmingService(
            IMonitorInfoManager monitorInfoManager,
            IDdcCiAccess bus,
            IOriginalBrightnessStore originalBrightnessStore,
            BrightnessFadeOptions? fadeOptions = null)
        {
            _monitors = monitorInfoManager;
            _bus = bus;
            _originals = originalBrightnessStore;
            _fade = fadeOptions ?? BrightnessFadeOptions.None;
        }

        private enum ChannelResult
        {
            Unreachable, // no channel could be opened, so nothing was written
            Failed,      // the channel opened but the work did not succeed
            Succeeded,
        }

        public async Task DimMonitorAsync(string? hardwareId, int percent)
        {
            if (hardwareId is not { Length: > 0 })
            {
                Log.Warning("Dim request without a hardware ID ignored.");
                return;
            }

            await RunExclusiveAsync(hardwareId, async () =>
            {
                await DimAsync(hardwareId, percent);
                return true;
            });
        }

        public Task UndimMonitorAsync(string hardwareId)
        {
            if (hardwareId is not { Length: > 0 })
            {
                Log.Warning("Undim request without a hardware ID ignored.");
                return Task.CompletedTask;
            }

            return RunExclusiveAsync(hardwareId, async () =>
            {
                await UndimAsync(hardwareId);
                return true;
            });
        }

        /// <summary>
        /// While the monitor is dimmed or blacked out only the level it will wake to is changed, so waking it lands
        /// on the scheduled brightness rather than the stale one.
        /// </summary>
        public Task<bool> SetScheduledBrightnessAsync(string hardwareId, int percent)
        {
            if (hardwareId is not { Length: > 0 }) return Task.FromResult(false);

            return RunExclusiveAsync(hardwareId, () => ApplyScheduledAsync(hardwareId, percent));
        }

        public Dictionary<string, uint> GetDimmedMonitors() => _originals.GetAll();

        // ----- Operations; each runs while holding the monitor's lock -----

        private async Task DimAsync(string hardwareId, int percent)
        {
            var monitor = await FindMonitorAsync(hardwareId);
            uint target = ScaleForMonitor(monitor, hardwareId, percent);

            UseChannel(monitor, hardwareId, channel =>
            {
                uint? before = ReadBrightness(channel, hardwareId);
                if (before is not uint current) return false;

                // Recorded before the first write, so a crash halfway through the fade can still be undone.
                _originals.RecordOriginal(hardwareId, current);
                return FadeTo(channel, hardwareId, current, target);
            });
        }

        private async Task UndimAsync(string hardwareId)
        {
            if (!_originals.TryGetOriginal(hardwareId, out uint original)) return;

            // An unconfirmed restore keeps the recording, so a reconnect, a settings save or the next start retries it.
            if (await TryRestoreAsync(hardwareId, original))
            {
                _originals.RemoveOriginal(hardwareId);
            }
        }

        private async Task<bool> ApplyScheduledAsync(string hardwareId, int percent)
        {
            var monitor = await FindMonitorAsync(hardwareId);
            uint target = ScaleForMonitor(monitor, hardwareId, percent);

            if (_originals.TryGetOriginal(hardwareId, out _))
            {
                _originals.ReplaceOriginal(hardwareId, target);
                Log.Information("Monitor {HardwareId} is dimmed now; it will wake up at the scheduled brightness {Brightness}.", hardwareId, target);
                return true;
            }

            var result = UseChannel(monitor, hardwareId, channel =>
            {
                if (channel.GetBrightness() is not uint current) return false;
                if (current == target) return true;

                Log.Information("Brightness schedule: monitor {HardwareId} goes from {From} to {To}.", hardwareId, current, target);
                return FadeTo(channel, hardwareId, current, target);
            });

            return result == ChannelResult.Succeeded;
        }

        private async Task<bool> TryRestoreAsync(string hardwareId, uint original)
        {
            for (int attempt = 1; ; attempt++)
            {
                var monitor = await FindMonitorAsync(hardwareId);
                var result = UseChannel(monitor, hardwareId, channel =>
                    WriteRestore(channel, hardwareId, original) && ReadsBackAs(channel, hardwareId, original));

                if (result == ChannelResult.Succeeded)
                {
                    Log.Information("Monitor {HardwareId} is back at its original brightness {Brightness}.", hardwareId, original);
                    return true;
                }

                if (result == ChannelResult.Unreachable)
                {
                    Log.Warning("Monitor {HardwareId} cannot be reached; its original brightness {Brightness} stays recorded for a later try.",
                        hardwareId, original);
                    return false;
                }

                if (attempt >= MaxRestoreTries)
                {
                    Log.Warning("Monitor {HardwareId} did not take brightness {Brightness} in {Tries} tries; the recording is kept.",
                        hardwareId, original, MaxRestoreTries);
                    return false;
                }

                await Task.Delay(RestoreRetryPause);
            }
        }

        // ----- Building blocks -----

        // DDC/CI transactions and fade pauses take a while, and callers include the idle loop (holding its own lock)
        // and the UI thread, so the work always moves to the thread pool before it queues on the monitor's lock.
        private Task<T> RunExclusiveAsync<T>(string hardwareId, Func<Task<T>> work) =>
            Task.Run(async () =>
            {
                var monitorLock = _monitorLocks.GetOrAdd(hardwareId, static _ => new SemaphoreSlim(1, 1));
                await monitorLock.WaitAsync();
                try
                {
                    return await work();
                }
                finally
                {
                    monitorLock.Release();
                }
            });

        private async Task<MonitorInfo?> FindMonitorAsync(string hardwareId)
        {
            var attached = await _monitors.GetCurrentMonitorsAsync();
            return attached.FirstOrDefault(monitor => monitor.HardwareId == hardwareId);
        }

        // Percent to the panel's own units, using the range it reported when it was last probed.
        private static uint ScaleForMonitor(MonitorInfo? monitor, string hardwareId, int percent)
        {
            uint range = monitor?.Capabilities?.MaxBrightness ?? 0;
            uint raw = BrightnessScale.ToRawBrightness(percent, range);
            if (raw != percent)
            {
                Log.Debug("{Percent}% is brightness {Raw} on monitor {HardwareId} (range 0-{Range}).", percent, raw, hardwareId, range);
            }

            return raw;
        }

        // The only place a channel is opened; it is always disposed again before returning.
        private ChannelResult UseChannel(MonitorInfo? monitor, string hardwareId, Func<IDdcCiSession, bool> work)
        {
            using var channel = monitor is null ? null : _bus.OpenSession(monitor.DeviceName);
            if (channel is null)
            {
                Log.Warning("No DDC/CI channel to monitor {HardwareId}.", hardwareId);
                return ChannelResult.Unreachable;
            }

            return work(channel) ? ChannelResult.Succeeded : ChannelResult.Failed;
        }

        // Walks from one level to the other in _fade.Steps writes, so the change is a fade instead of a jump.
        // An intermediate write that is refused ends the walk early; the final value is always attempted.
        private bool FadeTo(IDdcCiSession channel, string hardwareId, uint from, uint to)
        {
            int steps = Math.Max(1, _fade.Steps);
            if (from != to)
            {
                double distance = (double)to - from;
                for (int step = 1; step < steps; step++)
                {
                    uint level = (uint)Math.Round(from + distance * step / steps);
                    if (!channel.SetBrightness(level)) break;

                    Thread.Sleep(_fade.StepDelay);
                }
            }

            if (!channel.SetBrightness(to))
            {
                Log.Warning("Monitor {HardwareId} refused brightness {Brightness}.", hardwareId, to);
                return false;
            }

            Log.Information("Monitor {HardwareId} set to brightness {Brightness}.", hardwareId, to);
            return true;
        }

        private static bool WriteRestore(IDdcCiSession channel, string hardwareId, uint original)
        {
            if (!channel.SetBrightness(original))
            {
                Log.Warning("Monitor {HardwareId} refused its original brightness {Brightness}.", hardwareId, original);
                return false;
            }

            Log.Debug("Original brightness {Brightness} written to monitor {HardwareId}; checking the read-back.", original, hardwareId);
            return true;
        }

        private static bool ReadsBackAs(IDdcCiSession channel, string hardwareId, uint expected)
        {
            if (ReadBrightness(channel, hardwareId) is not uint actual) return false;

            uint gap = actual >= expected ? actual - expected : expected - actual;
            if (gap <= ReadBackTolerance) return true;

            Log.Warning("Monitor {HardwareId} reads back brightness {Actual} after being set to {Expected}.", hardwareId, actual, expected);
            return false;
        }

        private static uint? ReadBrightness(IDdcCiSession channel, string hardwareId)
        {
            uint? value = channel.GetBrightness();
            if (value is null)
            {
                Log.Warning("Could not read the brightness of monitor {HardwareId}.", hardwareId);
            }

            return value;
        }
    }
}
