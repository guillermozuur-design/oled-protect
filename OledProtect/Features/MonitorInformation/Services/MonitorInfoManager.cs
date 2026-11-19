using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorInformation.Services
{
    /// <summary>
    /// Keeps the last probed monitor list. Probing talks DDC/CI to every panel, so only one probe runs at a time
    /// and requests that queue behind it reuse its result when it is recent enough. Thread-safe.
    /// </summary>
    public sealed class MonitorInfoManager : IMonitorInfoManager
    {
        private readonly IMonitorInfoProvider _provider;

        // One probe at a time.
        private readonly SemaphoreSlim _probeTurn = new(1, 1);

        // Guards _demanded and _openRefreshes.
        private readonly object _books = new();

        // Every refresh that does not join a running one asks for a newer generation of the list.
        private int _demanded;
        private int _openRefreshes;

        // The last successful probe and the generation it satisfies; null until one succeeded.
        private volatile Snapshot? _snapshot;

        public MonitorInfoManager(IMonitorInfoProvider monitorInfoProvider) => _provider = monitorInfoProvider;

        public Task<IReadOnlyList<MonitorInfo>> GetCurrentMonitorsAsync()
        {
            int wanted;
            lock (_books) wanted = _demanded;

            return _snapshot is { } fresh && fresh.Generation >= wanted ? fresh.Ready : ObtainAsync(wanted);
        }

        public async Task<IReadOnlyList<MonitorInfo>> RefreshMonitorsAsync()
        {
            int wanted;
            lock (_books)
            {
                // A refresh that is still open answers this one too; otherwise ask for a newer list.
                if (_openRefreshes == 0) _demanded++;
                else Log.Debug("Monitor rescan requested while one is open; sharing its result.");

                wanted = _demanded;
                _openRefreshes++;
            }

            try
            {
                return await ObtainAsync(wanted).ConfigureAwait(false);
            }
            finally
            {
                lock (_books) _openRefreshes--;
            }
        }

        public List<MonitorInfo> GetLatestMonitorsBasicInfo() => _provider.GetAllMonitorsBasicInfo();

        public void EnrichMonitorInfoList(List<MonitorInfo>? readings)
        {
            if (readings is null) return;

            // Settings, state and dimming all key on the hardware ID; a monitor without one is dropped here.
            foreach (var nameless in readings.Where(r => string.IsNullOrEmpty(r.HardwareId)).ToList())
            {
                Log.Warning("{DeviceName} dropped from the monitor list: Windows gave it no hardware ID.", nameless.DeviceName);
                readings.Remove(nameless);
            }

            readings.ForEach(r => r.Capabilities = _provider.GetDdcCiCapabilities(r));
        }

        // Waits for the probe turn, then either takes a list that a probe finished meanwhile, or probes itself.
        // A failed probe leaves the snapshot alone; its caller gets the exception and the next request probes again.
        private async Task<IReadOnlyList<MonitorInfo>> ObtainAsync(int wanted)
        {
            await _probeTurn.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_snapshot is { } fresh && fresh.Generation >= wanted) return fresh.List;

                int generation;
                lock (_books) generation = _demanded;

                Log.Information("Probing the attached monitors (list generation {Generation}).", generation);
                try
                {
                    IReadOnlyList<MonitorInfo> list = await Task.Run(ProbeAll).ConfigureAwait(false);
                    _snapshot = new Snapshot(list, generation);
                    return list;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Monitor probe failed; nothing cached, the next request probes again.");
                    throw;
                }
            }
            finally
            {
                _probeTurn.Release();
            }
        }

        private IReadOnlyList<MonitorInfo> ProbeAll()
        {
            var list = _provider.GetAllMonitorsBasicInfo();
            EnrichMonitorInfoList(list);
            return list;
        }

        // A finished probe and the generation it was started for. Ready is created once, so every cache hit
        // hands out the same completed task.
        private sealed record Snapshot(IReadOnlyList<MonitorInfo> List, int Generation)
        {
            public Task<IReadOnlyList<MonitorInfo>> Ready { get; } = Task.FromResult(List);
        }
    }
}
