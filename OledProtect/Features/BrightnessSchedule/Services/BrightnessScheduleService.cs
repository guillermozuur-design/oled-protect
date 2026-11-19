using OledProtect.Features.MonitorDimming.Services.Interfaces;
using OledProtect.Features.MonitorInformation.Services.Interfaces;
using OledProtect.Features.UserSettings.Services.Interfaces;
using Serilog;

namespace OledProtect.Features.BrightnessSchedule.Services
{
    /// <summary>
    /// Checks every 30 seconds which brightness each monitor's schedule asks for, and writes it only when
    /// the scheduled level changes. A brightness you set by hand in between is left alone until the next
    /// step of the schedule.
    /// </summary>
    public class BrightnessScheduleService : IBrightnessScheduleService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        private readonly IMonitorSettingsFileService _settingsFileService;
        private readonly IMonitorInfoManager _monitorInfoManager;
        private readonly IMonitorDimmingService _dimmingService;
        private readonly GlobalBrightnessSchedule _schedule;
        private readonly Dictionary<string, int> _lastApplied = new();
        private readonly object _lock = new();
        private readonly SemaphoreSlim _checkGate = new(1, 1);
        private CancellationTokenSource? _cts;

        public BrightnessScheduleService(
            IMonitorSettingsFileService settingsFileService,
            IMonitorInfoManager monitorInfoManager,
            IMonitorDimmingService dimmingService,
            GlobalBrightnessSchedule schedule)
        {
            _schedule = schedule;
            _settingsFileService = settingsFileService;
            _monitorInfoManager = monitorInfoManager;
            _dimmingService = dimmingService;
        }

        public void Start()
        {
            Stop();
            var cts = new CancellationTokenSource();
            _cts = cts;
            _settingsFileService.SettingsChanged += OnSettingsChanged;
            _schedule.Changed += Reapply;
            Task.Run(() => LoopAsync(cts.Token));
            Log.Information("BrightnessScheduleService started.");
        }

        public void Stop()
        {
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts == null) return;
            _settingsFileService.SettingsChanged -= OnSettingsChanged;
            _schedule.Changed -= Reapply;
            cts.Cancel();
            cts.Dispose();
        }

        public void Reapply()
        {
            lock (_lock) _lastApplied.Clear();
            _ = Task.Run(CheckOnceAsync);
        }

        private void OnSettingsChanged(List<UserSettings.Models.MonitorSettings> _) => Reapply();

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await CheckOnceAsync();
                try { await Task.Delay(CheckInterval, token); }
                catch (OperationCanceledException) { return; }
            }
        }

        private async Task CheckOnceAsync()
        {
            if (!await _checkGate.WaitAsync(0)) return; // een check loopt al
            try
            {
                // One schedule for all screens (that can be set over DDC/CI).
                var target = _schedule.TargetAt(TimeOnly.FromDateTime(DateTime.Now));
                if (target == null) return;

                var monitors = await _monitorInfoManager.GetCurrentMonitorsAsync();
                foreach (var monitor in monitors.Where(m => m.Capabilities?.IsSupported == true && !string.IsNullOrEmpty(m.HardwareId)))
                {
                    lock (_lock)
                    {
                        if (_lastApplied.TryGetValue(monitor.HardwareId, out var last) && last == target.Value) continue;
                    }

                    // Only remember it when it worked: a monitor in standby or a failed DDC/CI write is retried
                    // on the next check (30 s) instead of staying wrong until the next step of the schedule.
                    if (await _dimmingService.SetScheduledBrightnessAsync(monitor.HardwareId, target.Value))
                    {
                        lock (_lock) _lastApplied[monitor.HardwareId] = target.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Brightness schedule check failed.");
            }
            finally
            {
                _checkGate.Release();
            }
        }
    }
}
