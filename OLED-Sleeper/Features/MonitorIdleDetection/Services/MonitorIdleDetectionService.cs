using OLED_Sleeper.Features.KeepAwakeApps;
using OLED_Sleeper.Features.MonitorBehavior.Commands;
using OLED_Sleeper.Features.MonitorIdleDetection.Models;
using OLED_Sleeper.Features.MonitorIdleDetection.Services.Interfaces;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.Messaging.Interfaces;
using Serilog;
using System.Windows;

namespace OLED_Sleeper.Features.MonitorIdleDetection.Services
{
    // Five times a second, looks at the desktop and moves every managed screen through
    // Awake -> Quiet (timer running) -> Asleep, sending the idle / active behaviour commands on the edges.
    public class MonitorIdleDetectionService : IMonitorIdleDetectionService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(200);

        // Scanning every top-level window five times a second would be wasteful; apps do not open or
        // move that fast.
        private static readonly TimeSpan AppScanInterval = TimeSpan.FromSeconds(1);

        private readonly IMediator _commands;
        private readonly GlobalIdleSettings _globalIdle;
        private readonly KeepAwakeAppsSettings? _keepAwakeApps;

        // Guards _screens and _paused: the check loop, settings updates and work mode all touch them.
        private readonly object _gate = new();
        private List<TrackedScreen> _screens = [];
        private bool _paused;

        private CancellationTokenSource? _loopCancel;

        // Last result of the keep-awake app scan; only the loop thread reads or writes these.
        private DateTime _nextAppScan = DateTime.MinValue;
        private bool _appKeepsEveryScreenAwake;
        private IReadOnlyList<Rect> _appScreenAreas = [];

        public MonitorIdleDetectionService(IMediator mediator, GlobalIdleSettings? globalIdle = null,
                                           KeepAwakeAppsSettings? keepAwakeApps = null)
        {
            _commands = mediator;
            _globalIdle = globalIdle ?? new GlobalIdleSettings();
            _keepAwakeApps = keepAwakeApps;
        }

        private enum Phase
        {
            Awake,
            Quiet,
            Asleep
        }

        // A managed screen joined with its geometry, plus where it is in the Awake/Quiet/Asleep cycle.
        private sealed class TrackedScreen(MonitorSettings settings, MonitorInfo monitor)
        {
            public MonitorSettings Settings { get; } = settings;
            public Rect Bounds { get; } = monitor.Bounds;
            public int DisplayNumber { get; } = monitor.DisplayNumber;
            public string Id => Settings.HardwareId;

            public Phase Phase { get; set; } = Phase.Awake;

            // When the screen last stopped being in use; the idle timer counts from here.
            public DateTime QuietSince { get; set; }
        }

        public void Start()
        {
            Stop();

            var cancel = new CancellationTokenSource();
            _loopCancel = cancel;
            _ = Task.Run(() => RunLoopAsync(cancel.Token));
            Log.Information("Idle detection running.");
        }

        public void Stop()
        {
            CancellationTokenSource? cancel = Interlocked.Exchange(ref _loopCancel, null);
            if (cancel is null)
            {
                return;
            }

            cancel.Cancel();
            cancel.Dispose();
            Log.Information("Idle detection stopped.");
        }

        public void SetPaused(bool paused)
        {
            lock (_gate)
            {
                if (_paused == paused)
                {
                    return;
                }

                _paused = paused;
                foreach (TrackedScreen screen in _screens)
                {
                    // Restoring is harmless for a screen that is not asleep, so wake all of them rather
                    // than trusting our own idea of which ones are.
                    if (paused)
                    {
                        _commands.SendAsync(new RestoreMonitorStateCommand { HardwareId = screen.Id });
                    }

                    screen.Phase = Phase.Awake;
                }
            }

            Log.Information(paused ? "Idle detection paused (work mode)." : "Idle detection resumed.");
        }

        // The caller hands in the screen list it already holds. Not reading the shared monitor cache here
        // means we never take that cache's lock while holding ours.
        public Task UpdateSettingsAsync(List<MonitorSettings> monitorSettings, IReadOnlyList<MonitorInfo> monitors)
        {
            var tracked = new List<TrackedScreen>();
            foreach (MonitorSettings setting in monitorSettings.Where(s => s.IsManaged))
            {
                tracked.AddRange(monitors.Where(m => m.HardwareId == setting.HardwareId)
                                         .Select(m => new TrackedScreen(setting, m)));
            }

            lock (_gate)
            {
                _screens = tracked;
            }

            Log.Information("Idle detection now tracks {Count} screen(s).", tracked.Count);
            return Task.CompletedTask;
        }

        // ---- the "still" limit, shared with the settings UI tests ----

        // Whether the pointer resting on a screen keeps it awake. With the "still" limit on, a pointer
        // that has not moved (and no key pressed) for longer than the limit no longer counts.
        public static bool MousePresenceCounts(MonitorSettings settings, bool cursorOnMonitor, uint systemIdleMilliseconds) =>
            settings.IsActiveOnMousePosition && cursorOnMonitor && WithinStillLimit(settings, systemIdleMilliseconds);

        // The "still" limit used by both the pointer and the focused-window condition: when switched on,
        // those only count while there was input within the last MouseStillMinutes (at least one).
        public static bool WithinStillLimit(MonitorSettings settings, uint systemIdleMilliseconds)
        {
            if (!settings.UseMouseStillLimit)
            {
                return true;
            }

            long limitMs = Math.Max(1, settings.MouseStillMinutes) * 60_000L;
            return systemIdleMilliseconds < limitMs;
        }

        // ---- loop ----

        private async Task RunLoopAsync(CancellationToken stop)
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    CheckOnce();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Idle check failed; trying again on the next round.");
                }

                try
                {
                    await Task.Delay(CheckInterval, stop);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void CheckOnce()
        {
            DesktopSnapshot desktop = DesktopProbe.Take();
            RescanKeepAwakeAppsIfDue();
            DateTime now = DateTime.UtcNow;

            lock (_gate)
            {
                if (_paused)
                {
                    return;
                }

                foreach (TrackedScreen screen in _screens)
                {
                    Step(screen, desktop, now);
                }
            }
        }

        private void RescanKeepAwakeAppsIfDue()
        {
            if (_keepAwakeApps is null || DateTime.UtcNow < _nextAppScan)
            {
                return;
            }

            _nextAppScan = DateTime.UtcNow + AppScanInterval;

            var rules = _keepAwakeApps.ActiveRules;
            if (rules.Count == 0)
            {
                _appKeepsEveryScreenAwake = false;
                _appScreenAreas = [];
                return;
            }

            (_appKeepsEveryScreenAwake, _appScreenAreas) = KeepAwakeEvaluator.Evaluate(AppWindowScanner.Scan(), rules);
        }

        // One tick of one screen's state machine.
        private void Step(TrackedScreen screen, DesktopSnapshot desktop, DateTime now)
        {
            Verdict verdict = Judge(screen, desktop);
            bool inUse = verdict.Reason != ActivityReason.None;

            // When a limit (not the user) ended the activity, the quiet period really began at the
            // last input, which can lie well in the past.
            DateTime quietSince = verdict.CutByLimit
                ? now - TimeSpan.FromMilliseconds(desktop.MillisecondsSinceInput)
                : now;

            switch (screen.Phase)
            {
                case Phase.Awake when !inUse:
                    screen.Phase = Phase.Quiet;
                    screen.QuietSince = quietSince;
                    SleepIfDue(screen, desktop, verdict, now);
                    break;

                case Phase.Quiet when inUse:
                    screen.Phase = Phase.Awake;
                    break;

                case Phase.Quiet:
                    if (verdict.CutByLimit && quietSince < screen.QuietSince)
                    {
                        screen.QuietSince = quietSince;
                    }

                    SleepIfDue(screen, desktop, verdict, now);
                    break;

                case Phase.Asleep when inUse:
                    TryWake(screen, desktop, verdict);
                    break;
            }
        }

        private void SleepIfDue(TrackedScreen screen, DesktopSnapshot desktop, Verdict verdict, DateTime now)
        {
            TimeSpan quietFor = now - screen.QuietSince;
            if (!verdict.ForceSleep && quietFor.TotalMilliseconds < screen.Settings.IdleTimeMilliseconds)
            {
                return;
            }

            screen.Phase = Phase.Asleep;
            Log.Information("Screen #{DisplayNumber} goes idle after {Seconds}s without use.",
                screen.DisplayNumber, Math.Round(quietFor.TotalSeconds));
            _commands.SendAsync(new ApplyMonitorIdleBehaviorCommand(Describe(screen, desktop, verdict.Reason)));
        }

        private void TryWake(TrackedScreen screen, DesktopSnapshot desktop, Verdict verdict)
        {
            var wake = Describe(screen, desktop, verdict.Reason);
            _commands.SendAsync(new ApplyMonitorActiveBehaviorCommand(wake));

            // The handler sets IsIgnored synchronously when the "activity" was only our own overlay
            // getting focus; then the screen stays asleep.
            if (wake.IsIgnored)
            {
                return;
            }

            screen.Phase = Phase.Awake;
            Log.Information("Screen #{DisplayNumber} is in use again ({Reason}).", screen.DisplayNumber, verdict.Reason);
        }

        private static MonitorIdleStateEventArgs Describe(TrackedScreen screen, DesktopSnapshot desktop, ActivityReason reason) =>
            new(screen.Id, screen.DisplayNumber, screen.Bounds, screen.Settings, desktop.FocusedWindow, reason);

        // ---- deciding whether a screen is in use ----

        // Reason: what keeps the screen awake (None = nothing). ForceSleep: the general timer ran out,
        // so the screen sleeps now instead of starting its own timer. CutByLimit: a limit, not the user,
        // ended the activity.
        private readonly record struct Verdict(ActivityReason Reason, bool ForceSleep, bool CutByLimit);

        private Verdict Judge(TrackedScreen screen, DesktopSnapshot desktop)
        {
            // A chosen app ("apps die het scherm aan houden") beats every timer and limit, like a video.
            if (_appKeepsEveryScreenAwake || KeepAwakeEvaluator.KeepsScreenAwake(screen.Bounds, _appScreenAreas))
            {
                return new Verdict(ActivityReason.KeepAwakeApp, ForceSleep: false, CutByLimit: false);
            }

            // General timer: after X minutes without any input nothing keeps any screen awake. A playing
            // video (display required) suspends it; the screen showing the video then stays on through its
            // focused window, the others follow their own timers.
            if (!desktop.DisplayRequired && _globalIdle.HasExpired(desktop.MillisecondsSinceInput))
            {
                return new Verdict(ActivityReason.None, ForceSleep: true, CutByLimit: true);
            }

            bool stillLimitHit = !desktop.DisplayRequired && !WithinStillLimit(screen.Settings, desktop.MillisecondsSinceInput);
            return new Verdict(FindActivity(screen, desktop), ForceSleep: false, CutByLimit: stillLimitHit);
        }

        // Conditions in priority order; the first that holds names the reason.
        private static ActivityReason FindActivity(TrackedScreen screen, DesktopSnapshot desktop)
        {
            MonitorSettings s = screen.Settings;

            bool typingOrMoving = s.IsActiveOnInput && desktop.MillisecondsSinceInput < s.IdleTimeMilliseconds;
            bool pointerHere = MousePresenceCounts(s, screen.Bounds.Contains(desktop.Cursor), desktop.EffectiveQuietMilliseconds);
            bool focusHere = s.IsActiveOnActiveWindow && Overlaps(screen.Bounds, desktop.FocusedWindowArea)
                             && (desktop.DisplayRequired || WithinStillLimit(s, desktop.MillisecondsSinceInput));

            return typingOrMoving ? ActivityReason.SystemInput
                 : pointerHere ? ActivityReason.MousePosition
                 : focusHere ? ActivityReason.ActiveWindow
                 : ActivityReason.None;
        }

        // A shared edge or corner is not overlap; the window has to cover some real area of the screen.
        private static bool Overlaps(Rect screen, Rect window)
        {
            Rect common = Rect.Intersect(screen, window);
            return !common.IsEmpty && common.Width > 0 && common.Height > 0;
        }
    }
}
