using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using Serilog;

namespace OledProtect.Features.MonitorInformation.Services
{
    /// <summary>
    /// Caches the probed monitor list and makes sure concurrent callers share one scan instead of each
    /// hammering the DDC/CI bus. Thread-safe.
    /// </summary>
    public sealed class MonitorInfoManager : IMonitorInfoManager
    {
        private readonly IMonitorInfoProvider _provider;
        private readonly object _gate = new();

        // The scan GetCurrentMonitorsAsync hands out. Null before the first scan and after a failed one.
        private Task<IReadOnlyList<MonitorInfo>>? _latestScan;

        // The scan a refresh started; a second refresh joins it while it is still running.
        private Task<IReadOnlyList<MonitorInfo>>? _pendingRefresh;

        public MonitorInfoManager(IMonitorInfoProvider monitorInfoProvider) => _provider = monitorInfoProvider;

        public Task<IReadOnlyList<MonitorInfo>> GetCurrentMonitorsAsync()
        {
            lock (_gate)
            {
                _latestScan ??= BeginScan();
                return _latestScan;
            }
        }

        public Task<IReadOnlyList<MonitorInfo>> RefreshMonitorsAsync()
        {
            lock (_gate)
            {
                if (_pendingRefresh is { IsCompleted: false } running)
                {
                    Log.Debug("A monitor rescan is already under way; this refresh request joins it.");
                    return running;
                }

                Log.Information("Rescanning the attached monitors on request.");
                _pendingRefresh = BeginScan();
                _latestScan = _pendingRefresh;
                return _pendingRefresh;
            }
        }

        public List<MonitorInfo> GetLatestMonitorsBasicInfo() => _provider.GetAllMonitorsBasicInfo();

        public void EnrichMonitorInfoList(List<MonitorInfo>? readings)
        {
            if (readings is null) return;

            // Everything downstream is keyed on the hardware ID, so a monitor without one cannot be managed.
            readings.RemoveAll(IsUnidentified);

            foreach (var monitor in readings)
            {
                monitor.Capabilities = _provider.GetDdcCiCapabilities(monitor);
            }
        }

        private static bool IsUnidentified(MonitorInfo monitor)
        {
            if (!string.IsNullOrEmpty(monitor.HardwareId)) return false;

            Log.Warning("Monitor {DeviceName} has no hardware ID and is left out; it will not be managed.", monitor.DeviceName);
            return true;
        }

        // Must run under _gate, so the caller has stored the task before a failure handler can look for it.
        private Task<IReadOnlyList<MonitorInfo>> BeginScan()
        {
            Task<IReadOnlyList<MonitorInfo>> scan = Task.Run(ScanAndProbe);
            _ = scan.ContinueWith(ForgetFailedScan, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return scan;
        }

        private IReadOnlyList<MonitorInfo> ScanAndProbe()
        {
            var readings = _provider.GetAllMonitorsBasicInfo();
            EnrichMonitorInfoList(readings);
            return readings;
        }

        // A failed scan must not stay cached, or every later caller would get the same exception.
        private void ForgetFailedScan(Task<IReadOnlyList<MonitorInfo>> failed)
        {
            Log.Error(failed.Exception?.GetBaseException(), "Scanning the monitors failed; the next request will scan again.");

            lock (_gate)
            {
                if (ReferenceEquals(_latestScan, failed)) _latestScan = null;
            }
        }
    }
}
