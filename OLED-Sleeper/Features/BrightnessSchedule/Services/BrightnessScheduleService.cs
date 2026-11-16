using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using OLED_Sleeper.Features.UserSettings.Services.Interfaces;
using Serilog;

namespace OLED_Sleeper.Features.BrightnessSchedule.Services
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
        private readonly Dictionary<string, int> _lastApplied = new();
        private readonly object _lock = new();
        private readonly SemaphoreSlim _checkGate = new(1, 1);
        private CancellationTokenSource? _cts;

        public BrightnessScheduleService(
            IMonitorSettingsFileService settingsFileService,
            IMonitorInfoManager monitorInfoManager,
            IMonitorDimmingService dimmingService)
        {
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
            Task.Run(() => LoopAsync(cts.Token));
            Log.Information("BrightnessScheduleService started.");
        }

        public void Stop()
        {
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts == null) return;
            _settingsFileService.SettingsChanged -= OnSettingsChanged;
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
                var settings = _settingsFileService.LoadSettings().Where(s => s.UseBrightnessSchedule).ToList();
                if (settings.Count == 0) return;

                var monitors = await _monitorInfoManager.GetCurrentMonitorsAsync();
                var now = TimeOnly.FromDateTime(DateTime.Now);

                foreach (var setting in settings)
                {
                    var monitor = monitors.FirstOrDefault(m => m.HardwareId == setting.HardwareId);
                    if (monitor?.Capabilities?.IsSupported != true) continue;

                    var target = BrightnessScheduleCalculator.GetTargetBrightness(setting.BrightnessSchedule, now);
                    if (target == null) continue;

                    lock (_lock)
                    {
                        if (_lastApplied.TryGetValue(setting.HardwareId, out var last) && last == target.Value) continue;
                    }

                    // Only remember it when it worked: a monitor in standby or a failed DDC/CI write is retried
                    // on the next check (30 s) instead of staying wrong until the next step of the schedule.
                    if (await _dimmingService.SetScheduledBrightnessAsync(setting.HardwareId, target.Value))
                    {
                        lock (_lock) _lastApplied[setting.HardwareId] = target.Value;
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
