using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.MonitorBehavior.Models;
using OledProtect.Features.MonitorInformation.Models;
using OledProtect.Features.UserSettings.Models;
using OledProtect.UI.Helpers;
using OledProtect.UI.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OledProtect.UI.ViewModels
{
    /// <summary>
    /// The editable settings of one screen, as shown in the per-screen panel. Remembers the last
    /// saved values so it can report unsaved edits and roll them back.
    /// </summary>
    public class MonitorConfigurationViewModel : ViewModelBase, IDataErrorInfo
    {
        private const string NoDimmingPrefix = "DIT SCHERM ONDERSTEUNT GEEN DIMMEN.\n\n";

        // Slider values come back as doubles with float noise; smaller differences are not edits.
        private const double DimTolerance = 0.0001;

        private static readonly string[] ManagedDependents =
            [nameof(IdleValueError), nameof(ActiveConditionsError), nameof(BehaviorError), nameof(MouseStillError)];

        private static readonly string[] BehaviorDependents =
            [nameof(IsDimSliderEnabled), nameof(IsLowerBrightnessOptionEnabled), nameof(LowerBrightnessTooltip), nameof(BehaviorError)];

        private static readonly string[] IdleDependents = [nameof(IdleValueError)];

        private static readonly string[] InputDependents = [nameof(ActiveConditionsError)];

        private static readonly string[] LocalWakeDependents =
            [nameof(MouseStillError), nameof(IsStillLimitApplicable), nameof(ActiveConditionsError)];

        private static readonly string[] StillLimitDependents = [nameof(MouseStillError)];

        private static readonly string[] NoDependents = [];

        private static readonly IReadOnlyList<BrightnessScheduleEntry> NoEntries = Array.Empty<BrightnessScheduleEntry>();

        private readonly MonitorInfo _monitor;

        private bool _managed;
        private MonitorBehaviorType _mode;
        private double _dim;
        private bool _blackoutLowersBrightness;
        private int? _idleAmount;
        private TimeUnit _idleUnit;
        private bool _wakeOnInput;
        private bool _wakeOnMouse;
        private bool _wakeOnFocusedWindow;
        private bool _useStillLimit;
        private int? _stillMinutes;

        // The per-screen brightness schedule is no longer edited here (one shared schedule lives in the
        // Algemeen tab), but older settings files still carry it, so it is passed through untouched.
        private bool _useSchedule;
        private IReadOnlyList<BrightnessScheduleEntry> _schedule = NoEntries;

        private State _saved;
        private bool _isDirty;

        public MonitorConfigurationViewModel(MonitorInfo monitorInfo)
        {
            _monitor = monitorInfo;
            _saved = StateFrom(new MonitorSettings());
            Load(_saved);
            RecomputeDirty();
        }

        public string MonitorTitle => MonitorTitleFormatter.Format(_monitor);

        /// <summary>Dimming needs DDC/CI; without it only the overlay-based behaviours make sense.</summary>
        public bool IsDimBehaviorEnabled => _monitor.Capabilities?.IsSupported == true;

        public IReadOnlyList<TimeUnit> TimeUnits { get; } = Enum.GetValues<TimeUnit>();

        public bool IsManaged
        {
            get => _managed;
            set => Change(ref _managed, value, ManagedDependents);
        }

        public MonitorBehaviorType Behavior
        {
            get => _mode;
            set
            {
                var previous = _mode;
                Change(ref _mode, value, BehaviorDependents);
                // A black screen has no brightness to speak of; reset the slider so it does not suggest one.
                if (value == MonitorBehaviorType.Blackout && previous != value) DimLevel = 0;
            }
        }

        public double DimLevel
        {
            get => _dim;
            set => Change(ref _dim, value, NoDependents);
        }

        /// <summary>Also drives the screen's own brightness to zero on blackout (DDC/CI screens only).</summary>
        public bool LowerBrightnessOnBlackout
        {
            get => _blackoutLowersBrightness;
            set => Change(ref _blackoutLowersBrightness, value, NoDependents);
        }

        public int? IdleValue
        {
            get => _idleAmount;
            set => Change(ref _idleAmount, value, IdleDependents);
        }

        public TimeUnit SelectedTimeUnit
        {
            get => _idleUnit;
            set => Change(ref _idleUnit, value, NoDependents);
        }

        public bool IsActiveOnInput
        {
            get => _wakeOnInput;
            set => Change(ref _wakeOnInput, value, InputDependents);
        }

        public bool IsActiveOnMousePosition
        {
            get => _wakeOnMouse;
            set => Change(ref _wakeOnMouse, value, LocalWakeDependents);
        }

        public bool IsActiveOnActiveWindow
        {
            get => _wakeOnFocusedWindow;
            set => Change(ref _wakeOnFocusedWindow, value, LocalWakeDependents);
        }

        public bool UseMouseStillLimit
        {
            get => _useStillLimit;
            set => Change(ref _useStillLimit, value, StillLimitDependents);
        }

        public int? MouseStillMinutes
        {
            get => _stillMinutes;
            set => Change(ref _stillMinutes, value, StillLimitDependents);
        }

        public bool IsDimSliderEnabled => _mode is MonitorBehaviorType.Dim;

        public bool IsLowerBrightnessOptionEnabled => IsDimBehaviorEnabled && Behavior == MonitorBehaviorType.Blackout;

        /// <summary>The "still" limit only refines the mouse and focused-window conditions.</summary>
        public bool IsStillLimitApplicable => IsActiveOnMousePosition || IsActiveOnActiveWindow;

        public string BehaviorSectionTooltip
        {
            get
            {
                const string explanation =
                    "Wat dit scherm doet als de wachttijd voorbij is.\n\n" +
                    "• Zwart: het scherm wordt helemaal zwart (OLED-pixels uit).\n" +
                    "• Dimmen: de helderheid van het scherm zelf gaat omlaag (DDC/CI).\n" +
                    "• Sterren: de echte sterrenhemel als screensaver.\n" +
                    "• Vliegtuigen: een donkere kaart met de echte vliegtuigen erboven.";
                return IsDimBehaviorEnabled ? explanation : NoDimmingPrefix + explanation;
            }
        }

        public string LowerBrightnessTooltip
        {
            get
            {
                const string explanation =
                    "Zet ook de helderheid van het scherm zelf op 0,\n" +
                    "naast het zwarte beeld.\n\n" +
                    "Vooral nuttig bij niet-OLED-schermen, waar de\n" +
                    "achtergrondverlichting anders blijft branden.";

                // Say first why the box is greyed out, if it is.
                var reason = !IsDimBehaviorEnabled ? NoDimmingPrefix
                    : Behavior != MonitorBehaviorType.Blackout ? "ALLEEN BIJ 'ZWART'.\n\n"
                    : string.Empty;
                return reason + explanation;
            }
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set => SetField(ref _isDirty, value);
        }

        // ---- Validation ----

        public string IdleValueError => this[nameof(IdleValue)];

        public string ActiveConditionsError => this[nameof(IsActiveOnInput)];

        public string BehaviorError => this[nameof(Behavior)];

        public string MouseStillError => StillLimitProblem() ?? string.Empty;

        public bool IsValid
        {
            get
            {
                if (ScheduleProblem() is not null || StillLimitProblem() is not null) return false;
                if (!IsManaged) return true;
                return IdleProblem() is null && WakeConditionProblem() is null && BehaviorProblem() is null;
            }
        }

        // IDataErrorInfo: only per-property messages are used.
        public string Error => string.Empty;

        public string this[string columnName] => IsManaged ? ProblemWith(columnName) ?? string.Empty : string.Empty;

        private string? ProblemWith(string propertyName) => propertyName switch
        {
            nameof(IdleValue) => IdleProblem(),
            nameof(Behavior) => BehaviorProblem(),
            nameof(IsActiveOnInput) or nameof(IsActiveOnMousePosition) or nameof(IsActiveOnActiveWindow) => WakeConditionProblem(),
            _ => null,
        };

        private string? IdleProblem() =>
            IdleValue is > 0 ? null : "Vul een wachttijd groter dan 0 in.";

        private string? BehaviorProblem() =>
            Behavior == MonitorBehaviorType.None ? "Kies wat er met dit scherm moet gebeuren." : null;

        private string? WakeConditionProblem() =>
            IsActiveOnInput || IsActiveOnMousePosition || IsActiveOnActiveWindow
                ? null
                : "Kies minstens één 'Wakker houden als'-optie.";

        private string? StillLimitProblem()
        {
            bool inUse = IsManaged && IsStillLimitApplicable && UseMouseStillLimit;
            return inUse && MouseStillMinutes is not (>= 1 and <= 600) ? "Kies 1 tot 600 minuten." : null;
        }

        // A stored schedule that is switched on must still make sense, even though it is not shown here.
        private string? ScheduleProblem()
        {
            if (!_useSchedule) return null;
            if (_schedule.Count == 0) return "Voeg minstens één tijd toe.";

            var times = _schedule.Select(e => BrightnessScheduleCalculator.ParseTime(e.Start)).ToList();
            if (times.Contains(null)) return "Gebruik tijden als 08:00 of 19:30.";
            return times.Distinct().Count() < times.Count ? "Elke tijd mag maar één keer voorkomen." : null;
        }

        // ---- Loading, saving and reverting ----

        /// <summary>Takes over a stored settings record. Does not change what counts as "saved".</summary>
        public void ApplySettings(MonitorSettings settings) => Load(StateFrom(settings));

        /// <summary>Accepts the current values as the saved baseline.</summary>
        public void MarkAsSaved()
        {
            _saved = Capture();
            RecomputeDirty();
        }

        /// <summary>Throws away unsaved edits.</summary>
        public void Revert()
        {
            Load(_saved);
            RecomputeDirty();
        }

        public MonitorSettings ToSettings()
        {
            var now = Capture();
            return new MonitorSettings
            {
                HardwareId = _monitor.HardwareId,
                IsManaged = now.IsManaged,
                Behavior = now.Behavior,
                DimLevel = now.DimLevel,
                LowerBrightnessOnBlackout = now.LowerBrightness,
                IdleValue = now.IdleValue,
                IdleUnit = now.IdleUnit,
                IsActiveOnInput = now.WakeOnInput,
                IsActiveOnMousePosition = now.WakeOnMouse,
                IsActiveOnActiveWindow = now.WakeOnFocusedWindow,
                UseMouseStillLimit = now.UseStillLimit,
                // An emptied field falls back to the stored default.
                MouseStillMinutes = now.StillMinutes ?? 10,
                UseBrightnessSchedule = now.UseSchedule,
                BrightnessSchedule = now.Schedule
                    .Select(e => new BrightnessScheduleEntry { Start = e.Start?.Trim() ?? string.Empty, Brightness = e.Brightness })
                    .ToList(),
            };
        }

        private void Change<T>(ref T field, T value, string[] dependents, [CallerMemberName] string? name = null)
        {
            if (!SetField(ref field, value, name)) return;

            foreach (var dependent in dependents) OnPropertyChanged(dependent);
            RecomputeDirty();
        }

        private void RecomputeDirty() => IsDirty = !Capture().SameAs(_saved);

        private State Capture() => new(
            IsManaged, Behavior, DimLevel, LowerBrightnessOnBlackout, IdleValue, SelectedTimeUnit,
            IsActiveOnInput, IsActiveOnMousePosition, IsActiveOnActiveWindow,
            UseMouseStillLimit, MouseStillMinutes, _useSchedule, _schedule);

        private State StateFrom(MonitorSettings s) => new(
            s.IsManaged, s.Behavior, s.DimLevel,
            // A stale "lower brightness" flag is meaningless once the screen lost DDC/CI.
            s.LowerBrightnessOnBlackout && IsDimBehaviorEnabled,
            s.IdleValue, s.IdleUnit,
            s.IsActiveOnInput, s.IsActiveOnMousePosition, s.IsActiveOnActiveWindow,
            s.UseMouseStillLimit, s.MouseStillMinutes,
            // The schedule flag is kept as stored, even when DDC/CI was not detected this time.
            s.UseBrightnessSchedule,
            (s.BrightnessSchedule ?? []).Select(e => new BrightnessScheduleEntry { Start = e.Start, Brightness = e.Brightness }).ToList());

        private void Load(State state)
        {
            IsManaged = state.IsManaged;
            // Behaviour first: switching to blackout zeroes the dim level, which the next line then overrides.
            Behavior = state.Behavior;
            DimLevel = state.DimLevel;
            LowerBrightnessOnBlackout = state.LowerBrightness;
            IdleValue = state.IdleValue;
            SelectedTimeUnit = state.IdleUnit;
            IsActiveOnInput = state.WakeOnInput;
            IsActiveOnMousePosition = state.WakeOnMouse;
            IsActiveOnActiveWindow = state.WakeOnFocusedWindow;
            UseMouseStillLimit = state.UseStillLimit;
            MouseStillMinutes = state.StillMinutes;
            _useSchedule = state.UseSchedule;
            _schedule = state.Schedule;
            RecomputeDirty();
        }

        /// <summary>One complete set of values, used both as the saved baseline and for comparisons.</summary>
        private sealed record State(
            bool IsManaged,
            MonitorBehaviorType Behavior,
            double DimLevel,
            bool LowerBrightness,
            int? IdleValue,
            TimeUnit IdleUnit,
            bool WakeOnInput,
            bool WakeOnMouse,
            bool WakeOnFocusedWindow,
            bool UseStillLimit,
            int? StillMinutes,
            bool UseSchedule,
            IReadOnlyList<BrightnessScheduleEntry> Schedule)
        {
            public bool SameAs(State other) =>
                // Compare everything except the two fields that need special handling, then those.
                this with { DimLevel = 0, Schedule = NoEntries } == other with { DimLevel = 0, Schedule = NoEntries }
                && Math.Abs(DimLevel - other.DimLevel) <= DimTolerance
                && Schedule.Select(Key).SequenceEqual(other.Schedule.Select(Key));

            private static string Key(BrightnessScheduleEntry e) => $"{e.Start}={e.Brightness}";
        }
    }
}
