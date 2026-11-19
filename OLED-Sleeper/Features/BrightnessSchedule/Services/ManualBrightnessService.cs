using OLED_Sleeper.Features.MonitorDimming.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Services.Interfaces;
using Serilog;
using System.Diagnostics.CodeAnalysis;

namespace OLED_Sleeper.Features.BrightnessSchedule.Services
{
    /// <summary>
    /// Brightness of all screens set by hand (from the quick panel, with the scroll wheel). It goes through the
    /// screens themselves (DDC/CI), like the brightness schedule, and holds until the schedule's next step.
    /// </summary>
    public static class ManualBrightness
    {
        public const int Step = 5, Lowest = 5, Highest = 100;

        /// <summary>The value after turning the wheel <paramref name="notches"/> notches (up = brighter).</summary>
        public static int Turn(int current, int notches)
        {
            int rounded = (int)Math.Round(current / (double)Step) * Step;
            return Math.Clamp(rounded + notches * Step, Lowest, Highest);
        }
    }

    [ExcludeFromCodeCoverage]
    public sealed class ManualBrightnessService
    {
        /// <summary>Scrolling quickly gives many steps: only the last one is sent to the screens.</summary>
        private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

        private readonly IMonitorDimmingService _dimming;
        private readonly IMonitorInfoManager _monitors;
        private readonly GlobalBrightnessSchedule _schedule;
        private int? _current;
        private CancellationTokenSource? _pending;

        public ManualBrightnessService(IMonitorDimmingService dimming, IMonitorInfoManager monitors, GlobalBrightnessSchedule schedule)
        {
            _dimming = dimming;
            _monitors = monitors;
            _schedule = schedule;
        }

        /// <summary>The brightness now: the last one chosen by hand, else what the schedule says, else 100%.</summary>
        public int Current
        {
            get
            {
                if (_current is { } c) return c;
                return _schedule.TargetAt(TimeOnly.FromDateTime(DateTime.Now)) ?? ManualBrightness.Highest;
            }
        }

        /// <summary>Turn the wheel: the new value is shown at once and sent to all screens a moment later.</summary>
        public int Turn(int notches)
        {
            int value = ManualBrightness.Turn(Current, notches);
            _current = value;
            _pending?.Cancel();
            var cts = _pending = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try { await Task.Delay(Settle, cts.Token); }
                catch (OperationCanceledException) { return; }
                await ApplyAsync(value);
            });
            return value;
        }

        private async Task ApplyAsync(int percent)
        {
            try
            {
                var monitors = await _monitors.GetCurrentMonitorsAsync();
                foreach (var m in monitors.Where(m => m.Capabilities?.IsSupported == true))
                    await _dimming.SetScheduledBrightnessAsync(m.HardwareId, percent);
                Log.Information("Brightness set by hand to {Percent}%.", percent);
            }
            catch (Exception ex)
            {
                Log.Warning("Brightness could not be set: {Message}", ex.Message);
            }
        }
    }
}
