using OledProtect.Features.MonitorDimming.Helpers;
using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorDimming.Services
{
    /// <summary>
    /// Talks brightness to the panels over DDC/CI: dim, wake and follow the brightness schedule.
    /// Requests for one panel form a first-come-first-served queue; separate panels never wait on each other.
    /// </summary>
    public sealed class MonitorDimmingService : IMonitorDimmingService
    {
        // Waking panels are slow to accept writes, so a wake gets a few rounds of write-then-verify.
        private const int WakeRounds = 3;
        private static readonly TimeSpan PauseBetweenRounds = TimeSpan.FromMilliseconds(200);

        // How far the verify read may drift from the written value (panels round to their own step size).
        private const uint VerifySlack = 2;

        private readonly IMonitorInfoManager _screens;
        private readonly IDdcCiAccess _ddc;
        private readonly IOriginalBrightnessStore _memory;
        private readonly BrightnessFadeOptions _glide;

        // Per panel: the last queued request. A new request starts once that one has finished, however it ended.
        private readonly Dictionary<string, Task> _queueTails = new(StringComparer.Ordinal);

        public MonitorDimmingService(
            IMonitorInfoManager monitorInfoManager,
            IDdcCiAccess bus,
            IOriginalBrightnessStore originalBrightnessStore,
            BrightnessFadeOptions? fadeOptions = null)
        {
            _screens = monitorInfoManager;
            _ddc = bus;
            _memory = originalBrightnessStore;
            _glide = fadeOptions ?? BrightnessFadeOptions.None;
        }

        public Task DimMonitorAsync(string? hardwareId, int percent)
        {
            if (string.IsNullOrEmpty(hardwareId))
            {
                Log.Warning("Dim skipped: the request named no monitor.");
                return Task.CompletedTask;
            }

            return Enqueue(hardwareId, () => DimNowAsync(hardwareId, percent));
        }

        public Task UndimMonitorAsync(string hardwareId)
        {
            if (string.IsNullOrEmpty(hardwareId))
            {
                Log.Warning("Wake skipped: the request named no monitor.");
                return Task.CompletedTask;
            }

            return Enqueue(hardwareId, () => WakeNowAsync(hardwareId));
        }

        /// <summary>
        /// A dimmed (or blacked-out) panel is left dark; only the level it returns to is moved to the schedule's value.
        /// </summary>
        public Task<bool> SetScheduledBrightnessAsync(string hardwareId, int percent) =>
            string.IsNullOrEmpty(hardwareId)
                ? Task.FromResult(false)
                : Enqueue(hardwareId, () => ScheduleNowAsync(hardwareId, percent));

        public Dictionary<string, uint> GetDimmedMonitors() => _memory.GetAll();

        // ===== The three requests (always running at the head of their panel's queue) =====

        private async Task<bool> DimNowAsync(string id, int percent)
        {
            var screen = await LocateAsync(id).ConfigureAwait(false);
            uint goal = PanelUnits(screen, id, percent);

            using var link = Connect(screen, id);
            if (link is null) return false;

            if (link.GetBrightness() is not uint startLevel)
            {
                Log.Warning("Dim of {HardwareId} skipped: the panel did not report its current level.", id);
                return false;
            }

            // Saved first: if the app dies mid-fade, the next start still knows where to go back to.
            _memory.RecordOriginal(id, startLevel);
            return Glide(link, id, startLevel, goal);
        }

        private async Task<bool> WakeNowAsync(string id)
        {
            if (!_memory.TryGetOriginal(id, out uint savedLevel)) return false;

            bool confirmed = await WriteVerifiedAsync(id, savedLevel).ConfigureAwait(false);

            // Without confirmation the saved level stays, so a later wake (reconnect, settings save, restart) retries.
            if (confirmed) _memory.RemoveOriginal(id);
            return confirmed;
        }

        private async Task<bool> ScheduleNowAsync(string id, int percent)
        {
            var screen = await LocateAsync(id).ConfigureAwait(false);
            uint goal = PanelUnits(screen, id, percent);

            if (_memory.TryGetOriginal(id, out _))
            {
                _memory.ReplaceOriginal(id, goal);
                Log.Information("Schedule: {HardwareId} is dark at the moment; it will come back at level {Level}.", id, goal);
                return true;
            }

            using var link = Connect(screen, id);
            if (link?.GetBrightness() is not uint level) return false;
            if (level == goal) return true;

            Log.Information("Schedule: moving {HardwareId} from level {Old} to {New}.", id, level, goal);
            return Glide(link, id, level, goal);
        }

        // ===== Write, read back, and try again =====

        // Each round looks the monitor up afresh, writes the level and reads it back. An unreachable panel ends
        // the attempt at once (retrying cannot help); a refused write or a wrong read-back earns another round.
        private async Task<bool> WriteVerifiedAsync(string id, uint level)
        {
            for (int round = 1; round <= WakeRounds; round++)
            {
                if (round > 1) await Task.Delay(PauseBetweenRounds).ConfigureAwait(false);

                using var link = Connect(await LocateAsync(id).ConfigureAwait(false), id);
                if (link is null)
                {
                    Log.Warning("Wake of {HardwareId} postponed: no DDC/CI link; level {Level} stays saved.", id, level);
                    return false;
                }

                string? complaint =
                    !link.SetBrightness(level) ? "rejected the write"
                    : link.GetBrightness() is not uint echo ? "gave no answer when read back"
                    : Spread(echo, level) > VerifySlack ? $"read back as {echo}"
                    : null;

                if (complaint is null)
                {
                    Log.Information("Wake of {HardwareId} verified at level {Level}.", id, level);
                    return true;
                }

                Log.Warning("Wake of {HardwareId}, round {Round}/{Rounds}: level {Level} {Complaint}.",
                    id, round, WakeRounds, level, complaint);
            }

            Log.Warning("Wake of {HardwareId} not verified after {Rounds} rounds; level {Level} stays saved.", id, WakeRounds, level);
            return false;
        }

        // ===== Shared plumbing =====

        // Chains the request behind the panel's previous one and always runs it on the thread pool: fades block
        // their thread between writes, and callers include the UI thread and the idle loop.
        private Task<T> Enqueue<T>(string id, Func<Task<T>> request)
        {
            lock (_queueTails)
            {
                Task before = _queueTails.GetValueOrDefault(id) ?? Task.CompletedTask;
                Task<T> mine = Task.Run(async () =>
                {
                    await Task.WhenAny(before).ConfigureAwait(false); // its failure belongs to its own caller
                    return await request().ConfigureAwait(false);
                });

                _queueTails[id] = mine;
                return mine;
            }
        }

        private async Task<MonitorInfo?> LocateAsync(string id)
        {
            foreach (var screen in await _screens.GetCurrentMonitorsAsync().ConfigureAwait(false))
            {
                if (screen.HardwareId == id) return screen;
            }

            return null;
        }

        // Null when the monitor is gone or its panel cannot be reached; the caller disposes what it gets.
        private IDdcCiSession? Connect(MonitorInfo? screen, string id)
        {
            var link = screen is null ? null : _ddc.OpenSession(screen.DeviceName);
            if (link is null) Log.Warning("{HardwareId}: no DDC/CI link available.", id);
            return link;
        }

        // Converts a percentage to the panel's own scale, as reported by its last probe.
        private static uint PanelUnits(MonitorInfo? screen, string id, int percent)
        {
            uint top = screen?.Capabilities?.MaxBrightness ?? 0;
            uint units = BrightnessScale.ToRawBrightness(percent, top);
            if (units != percent)
            {
                Log.Debug("{HardwareId}: {Percent} percent = level {Units} of {Top}.", id, percent, units, top);
            }

            return units;
        }

        // Moves the panel in _glide.Steps writes. The in-between levels are best effort (the first refusal skips
        // the rest of them); the goal itself is always written, and only its outcome counts.
        private bool Glide(IDdcCiSession link, string id, uint from, uint to)
        {
            int stepCount = Math.Max(1, _glide.Steps);
            int inBetween = from == to ? 0 : stepCount - 1;
            double span = (double)to - from;

            for (int k = 1; k <= inBetween && link.SetBrightness((uint)Math.Round(from + span * k / stepCount)); k++)
            {
                Thread.Sleep(_glide.StepDelay);
            }

            bool landed = link.SetBrightness(to);
            if (landed) Log.Information("{HardwareId} now at level {Level}.", id, to);
            else Log.Warning("{HardwareId} rejected level {Level}.", id, to);
            return landed;
        }

        private static uint Spread(uint a, uint b) => a > b ? a - b : b - a;
    }
}
