using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.Features.MonitorInformation.Models;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.UI.Helpers;
using OLED_Sleeper.UI.Models;
using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.UI.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace OLED_Sleeper.UI.ViewModels
{
    /// <summary>
    /// ViewModel for configuring an individual monitor's behavior and settings.
    /// Handles state management, validation, and dirty tracking for the monitor configuration UI.
    /// </summary>
    public class MonitorConfigurationViewModel : ViewModelBase, IDataErrorInfo
    {
        /// <summary>
        /// The underlying monitor information model.
        /// </summary>
        private readonly MonitorInfo _monitorInfo;

        #region Properties

        /// <summary>
        /// The display title for the monitor, including primary indicator if applicable. A duplicated surface
        /// is titled the way Windows titles it, such as <c>Monitor 1|2</c>.
        /// </summary>
        public string MonitorTitle => MonitorTitleFormatter.Format(_monitorInfo);

        public string IdleValueError => this["IdleValue"];

        public string ActiveConditionsError => this["IsActiveOnInput"];

        // --- IsManaged ---
        private bool _isManaged;

        private bool _initialIsManaged;

        /// <summary>
        /// Gets or sets whether this monitor is managed by the application.
        /// </summary>
        public bool IsManaged
        {
            get => _isManaged;
            set
            {
                _isManaged = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IdleValueError));
                OnPropertyChanged(nameof(ActiveConditionsError));
                OnPropertyChanged(nameof(BehaviorError)); // Ensure all errors update
                UpdateDirtyState();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the "Dim" behavior option should be enabled.
        /// </summary>
        public bool IsDimBehaviorEnabled => _monitorInfo.Capabilities?.IsSupported == true;

        /// <summary>
        /// Gets the tooltip for the entire Behavior section, changing based on DDC/CI support.
        /// </summary>
        public string BehaviorSectionTooltip
        {
            get
            {
                string baseTooltip = "Choose how the monitor reacts after the idle timer expires.\n\n" +
                                    "• Blackout: Turns the monitor completely black.\n" +
                                     "• Dim: Reduces the monitor's brightness using DDC/CI.\n" +
                                     "• Sterren: OLED-vriendelijke screensaver met vallende sterren.";

                if (_monitorInfo.Capabilities?.IsSupported != true)
                {
                    baseTooltip = "THE SELECTED MONITOR DOES NOT SUPPORT DIMMING.\n\n" + baseTooltip;
                }

                return baseTooltip;
            }
        }

        // --- Behavior ---
        private MonitorBehaviorType _behavior;

        private MonitorBehaviorType _initialBehavior;

        /// <summary>
        /// Gets or sets the behavior for this monitor (e.g., Dim, Blackout).
        /// </summary>
        public MonitorBehaviorType Behavior
        {
            get => _behavior;
            set
            {
                _behavior = value;
                if (_behavior == MonitorBehaviorType.Blackout) { DimLevel = 0; }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDimSliderEnabled));
                OnPropertyChanged(nameof(IsLowerBrightnessOptionEnabled));
                OnPropertyChanged(nameof(LowerBrightnessTooltip));
                OnPropertyChanged(nameof(BehaviorError));
                UpdateDirtyState();
            }
        }

        /// <summary>
        /// Returns an error message if the behavior is invalid.
        /// </summary>
        public string BehaviorError => this["Behavior"];

        // --- DimLevel ---
        private double _dimLevel;

        private double _initialDimLevel;

        /// <summary>
        /// Gets or sets the dimming level for the monitor (0-100).
        /// </summary>
        public double DimLevel
        {
            get => _dimLevel;
            set { _dimLevel = value; OnPropertyChanged(); UpdateDirtyState(); }
        }

        /// <summary>
        /// Returns true if the dim slider should be enabled (only for Dim behavior).
        /// </summary>
        public bool IsDimSliderEnabled => Behavior == MonitorBehaviorType.Dim;

        // --- LowerBrightnessOnBlackout ---
        private bool _lowerBrightnessOnBlackout;

        private bool _initialLowerBrightnessOnBlackout;

        /// <summary>
        /// Gets or sets whether a blackout also sets this monitor's hardware brightness to zero.
        /// Always false on a monitor without DDC/CI support.
        /// </summary>
        public bool LowerBrightnessOnBlackout
        {
            get => _lowerBrightnessOnBlackout;
            set
            {
                _lowerBrightnessOnBlackout = value;
                OnPropertyChanged();
                UpdateDirtyState();
            }
        }

        /// <summary>
        /// Returns true if the lower-brightness option should be enabled (only for Blackout behavior on a monitor that supports DDC/CI).
        /// </summary>
        public bool IsLowerBrightnessOptionEnabled => Behavior == MonitorBehaviorType.Blackout && IsDimBehaviorEnabled;

        /// <summary>
        /// Gets the tooltip for the lower-brightness option, prefixed with the reason when the option is disabled.
        /// </summary>
        public string LowerBrightnessTooltip
        {
            get
            {
                string baseTooltip = "Takes the monitor's own brightness down to zero\n" +
                                     "as well as covering the screen with the black overlay.\n\n" +
                                     "Useful on non-OLED displays, where the backlight\n" +
                                     "stays lit behind the black overlay.";

                if (!IsDimBehaviorEnabled)
                    return "THE SELECTED MONITOR DOES NOT SUPPORT DIMMING.\n\n" + baseTooltip;

                if (Behavior != MonitorBehaviorType.Blackout)
                    return "AVAILABLE ONLY WHEN BLACKOUT IS SELECTED.\n\n" + baseTooltip;

                return baseTooltip;
            }
        }

        // --- Idle Timer ---
        /// <summary>
        /// List of available time units for idle timer.
        /// </summary>
        public List<TimeUnit> TimeUnits { get; } = Enum.GetValues(typeof(TimeUnit)).Cast<TimeUnit>().ToList();

        private int? _idleValue;
        private int? _initialIdleValue;

        /// <summary>
        /// Gets or sets the idle time value before the monitor behavior triggers.
        /// </summary>
        public int? IdleValue
        {
            get => _idleValue;
            set
            {
                _idleValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IdleValueError));
                UpdateDirtyState();
            }
        }

        private TimeUnit _selectedTimeUnit;
        private TimeUnit _initialSelectedTimeUnit;

        /// <summary>
        /// Gets or sets the selected time unit for the idle timer.
        /// </summary>
        public TimeUnit SelectedTimeUnit
        {
            get => _selectedTimeUnit;
            set { _selectedTimeUnit = value; OnPropertyChanged(); UpdateDirtyState(); }
        }

        // --- Active Conditions ---
        private bool _isActiveOnInput;

        private bool _initialIsActiveOnInput;

        /// <summary>
        /// Gets or sets whether keyboard/mouse input keeps the monitor active.
        /// </summary>
        public bool IsActiveOnInput
        {
            get => _isActiveOnInput;
            set
            {
                _isActiveOnInput = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveConditionsError));
                UpdateDirtyState();
            }
        }

        private bool _isActiveOnMousePosition;
        private bool _initialIsActiveOnMousePosition;

        /// <summary>
        /// Gets or sets whether mouse position keeps the monitor active.
        /// </summary>
        public bool IsActiveOnMousePosition
        {
            get => _isActiveOnMousePosition;
            set
            {
                _isActiveOnMousePosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MouseStillError));
                OnPropertyChanged(nameof(ActiveConditionsError));
                UpdateDirtyState();
            }
        }

        private bool _isActiveOnActiveWindow;
        private bool _initialIsActiveOnActiveWindow;

        /// <summary>
        /// Gets or sets whether the active window keeps the monitor active.
        /// </summary>
        public bool IsActiveOnActiveWindow
        {
            get => _isActiveOnActiveWindow;
            set
            {
                _isActiveOnActiveWindow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveConditionsError));
                UpdateDirtyState();
            }
        }

        // --- Mouse still limit ---
        private bool _useMouseStillLimit;
        private bool _initialUseMouseStillLimit;
        private int? _mouseStillMinutes;
        private int? _initialMouseStillMinutes;

        public bool UseMouseStillLimit
        {
            get => _useMouseStillLimit;
            set { _useMouseStillLimit = value; OnPropertyChanged(); OnPropertyChanged(nameof(MouseStillError)); UpdateDirtyState(); }
        }

        public int? MouseStillMinutes
        {
            get => _mouseStillMinutes;
            set { _mouseStillMinutes = value; OnPropertyChanged(); OnPropertyChanged(nameof(MouseStillError)); UpdateDirtyState(); }
        }

        public string MouseStillError => ValidateMouseStill() ?? string.Empty;

        private string? ValidateMouseStill()
        {
            if (!IsManaged || !IsActiveOnMousePosition || !UseMouseStillLimit) return null;
            if (MouseStillMinutes is null or < 1 or > 600) return "Kies 1 tot 600 minuten.";
            return null;
        }

        // --- Brightness schedule ---
        private bool _useBrightnessSchedule;
        private bool _initialUseBrightnessSchedule;
        private string _initialScheduleSignature = string.Empty;
        private bool _suppressScheduleChanges;

        public bool UseBrightnessSchedule
        {
            get => _useBrightnessSchedule;
            set
            {
                _useBrightnessSchedule = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScheduleError));
                UpdateDirtyState();
            }
        }

        public ObservableCollection<ScheduleEntryViewModel> ScheduleEntries { get; } = new();

        public ICommand AddScheduleEntryCommand { get; }

        public ICommand RemoveScheduleEntryCommand { get; }

        public string ScheduleError => ValidateSchedule() ?? string.Empty;

        public string ScheduleTooltip =>
            (IsDimBehaviorEnabled ? "" : "DIT SCHERM ONDERSTEUNT GEEN DDC/CI-HELDERHEID.\n\n") +
            "Stel per tijdstip de helderheid van dit scherm in.\n" +
            "Vanaf elke tijd geldt die helderheid, tot de volgende tijd.\n" +
            "Na de laatste tijd van de dag loopt hij door tot de eerste van de volgende dag.\n\n" +
            "Een helderheid die je zelf op het scherm verandert, blijft staan\n" +
            "tot het volgende tijdstip in het schema.";

        #endregion Properties

        /// <param name="monitorInfo">The monitor information model.</param>
        public MonitorConfigurationViewModel(MonitorInfo monitorInfo)
        {
            _monitorInfo = monitorInfo;

            AddScheduleEntryCommand = new RelayCommand(AddScheduleEntry);
            RemoveScheduleEntryCommand = new RelayCommand(p =>
            {
                if (p is ScheduleEntryViewModel entry && ScheduleEntries.Remove(entry)) OnScheduleChanged();
            });

            ApplySettings(new MonitorSettings());
            MarkAsSaved();
        }

        #region State Management (Dirty Tracking, Saving, Loading)

        /// <summary>
        /// Gets whether the configuration has unsaved changes.
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Updates the IsDirty property based on whether any tracked property has changed from its initial value.
        /// Uses a tolerance for floating point comparison.
        /// </summary>
        private void UpdateDirtyState()
        {
            const double epsilon = 0.0001;
            IsDirty = IsManaged != _initialIsManaged ||
                       Behavior != _initialBehavior ||
                       Math.Abs(DimLevel - _initialDimLevel) > epsilon ||
                       LowerBrightnessOnBlackout != _initialLowerBrightnessOnBlackout ||
                       IdleValue != _initialIdleValue ||
                       SelectedTimeUnit != _initialSelectedTimeUnit ||
                       IsActiveOnInput != _initialIsActiveOnInput ||
                       IsActiveOnMousePosition != _initialIsActiveOnMousePosition ||
                       IsActiveOnActiveWindow != _initialIsActiveOnActiveWindow ||
                       UseMouseStillLimit != _initialUseMouseStillLimit ||
                       MouseStillMinutes != _initialMouseStillMinutes ||
                       UseBrightnessSchedule != _initialUseBrightnessSchedule ||
                       ScheduleSignature() != _initialScheduleSignature;

            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Marks the current state as saved, updating the initial values for dirty tracking.
        /// </summary>
        public void MarkAsSaved()
        {
            _initialIsManaged = IsManaged;
            _initialBehavior = Behavior;
            _initialDimLevel = DimLevel;
            _initialLowerBrightnessOnBlackout = LowerBrightnessOnBlackout;
            _initialIdleValue = IdleValue;
            _initialSelectedTimeUnit = SelectedTimeUnit;
            _initialIsActiveOnInput = IsActiveOnInput;
            _initialIsActiveOnMousePosition = IsActiveOnMousePosition;
            _initialIsActiveOnActiveWindow = IsActiveOnActiveWindow;
            _initialUseMouseStillLimit = UseMouseStillLimit;
            _initialMouseStillMinutes = MouseStillMinutes;
            _initialUseBrightnessSchedule = UseBrightnessSchedule;
            _initialScheduleSignature = ScheduleSignature();
            UpdateDirtyState();
        }

        /// <summary>
        /// Returns every tracked property to the value it had when the configuration was last saved.
        /// </summary>
        public void Revert()
        {
            IsManaged = _initialIsManaged;
            Behavior = _initialBehavior;
            DimLevel = _initialDimLevel;
            LowerBrightnessOnBlackout = _initialLowerBrightnessOnBlackout;
            IdleValue = _initialIdleValue;
            SelectedTimeUnit = _initialSelectedTimeUnit;
            IsActiveOnInput = _initialIsActiveOnInput;
            IsActiveOnMousePosition = _initialIsActiveOnMousePosition;
            IsActiveOnActiveWindow = _initialIsActiveOnActiveWindow;
            UseMouseStillLimit = _initialUseMouseStillLimit;
            MouseStillMinutes = _initialMouseStillMinutes;
            UseBrightnessSchedule = _initialUseBrightnessSchedule;
            LoadSchedule(ParseSignature(_initialScheduleSignature));
            UpdateDirtyState();
        }

        /// <summary>
        /// Applies settings from a MonitorSettings model to this ViewModel.
        /// </summary>
        /// <param name="settings">The settings to apply.</param>
        public void ApplySettings(MonitorSettings settings)
        {
            IsManaged = settings.IsManaged;
            Behavior = settings.Behavior;
            DimLevel = settings.DimLevel;
            LowerBrightnessOnBlackout = settings.LowerBrightnessOnBlackout && IsDimBehaviorEnabled;
            IdleValue = settings.IdleValue;
            SelectedTimeUnit = settings.IdleUnit;
            IsActiveOnInput = settings.IsActiveOnInput;
            IsActiveOnMousePosition = settings.IsActiveOnMousePosition;
            IsActiveOnActiveWindow = settings.IsActiveOnActiveWindow;
            UseMouseStillLimit = settings.UseMouseStillLimit;
            MouseStillMinutes = settings.MouseStillMinutes;
            UseBrightnessSchedule = settings.UseBrightnessSchedule && IsDimBehaviorEnabled;
            LoadSchedule(settings.BrightnessSchedule);
        }

        private void LoadSchedule(IEnumerable<BrightnessScheduleEntry> entries)
        {
            _suppressScheduleChanges = true;
            ScheduleEntries.Clear();
            foreach (var entry in entries)
            {
                ScheduleEntries.Add(new ScheduleEntryViewModel(entry, OnScheduleChanged));
            }
            _suppressScheduleChanges = false;
            OnScheduleChanged();
        }

        private void AddScheduleEntry()
        {
            var last = ScheduleEntries.LastOrDefault();
            var lastTime = last == null ? null : BrightnessScheduleCalculator.ParseTime(last.Start);
            var start = lastTime?.AddHours(2) ?? new TimeOnly(8, 0);
            ScheduleEntries.Add(new ScheduleEntryViewModel(
                new BrightnessScheduleEntry { Start = start.ToString("HH:mm"), Brightness = last?.Brightness ?? 70 },
                OnScheduleChanged));
            OnScheduleChanged();
        }

        private void OnScheduleChanged()
        {
            if (_suppressScheduleChanges) return;
            OnPropertyChanged(nameof(ScheduleError));
            UpdateDirtyState();
        }

        private string ScheduleSignature() =>
            string.Join(";", ScheduleEntries.Select(e => $"{e.Start}={e.Brightness}"));

        private static List<BrightnessScheduleEntry> ParseSignature(string signature) =>
            signature.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('='))
                .Where(kv => kv.Length == 2)
                .Select(kv => new BrightnessScheduleEntry { Start = kv[0], Brightness = int.TryParse(kv[1], out var b) ? b : 70 })
                .ToList();

        private string? ValidateSchedule()
        {
            if (!UseBrightnessSchedule) return null;
            if (ScheduleEntries.Count == 0) return "Voeg minstens één tijd toe.";
            if (ScheduleEntries.Any(e => !e.IsStartValid)) return "Gebruik tijden als 08:00 of 19:30.";
            var times = ScheduleEntries.Select(e => BrightnessScheduleCalculator.ParseTime(e.Start)).ToList();
            if (times.Distinct().Count() != times.Count) return "Elke tijd mag maar één keer voorkomen.";
            return null;
        }

        /// <summary>
        /// Converts the current ViewModel state to a MonitorSettings model.
        /// </summary>
        /// <returns>A MonitorSettings object representing the current configuration.</returns>
        public MonitorSettings ToSettings()
        {
            return new MonitorSettings
            {
                HardwareId = _monitorInfo.HardwareId,
                IsManaged = IsManaged,
                Behavior = Behavior,
                DimLevel = DimLevel,
                LowerBrightnessOnBlackout = LowerBrightnessOnBlackout,
                IdleValue = IdleValue,
                IdleUnit = SelectedTimeUnit,
                IsActiveOnInput = IsActiveOnInput,
                IsActiveOnMousePosition = IsActiveOnMousePosition,
                IsActiveOnActiveWindow = IsActiveOnActiveWindow,
                UseMouseStillLimit = UseMouseStillLimit,
                MouseStillMinutes = MouseStillMinutes ?? 10,
                UseBrightnessSchedule = UseBrightnessSchedule,
                BrightnessSchedule = ScheduleEntries.Select(e => e.ToEntry()).ToList()
            };
        }

        #endregion State Management (Dirty Tracking, Saving, Loading)

        #region Validation

        /// <summary>
        /// Gets whether the current configuration is valid according to all validation rules.
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (ValidateSchedule() != null) return false;
                if (ValidateMouseStill() != null) return false;
                if (!IsManaged) return true; // Unmanaged monitors are always "valid"

                return ValidateIdleValue() == null && ValidateActiveConditions() == null && ValidateBehavior() == null;
            }
        }

        /// <summary>
        /// Gets the error message for the entire object. Not used in this implementation.
        /// </summary>
        public string Error => string.Empty;

        /// <summary>
        /// Gets the error message for the property with the given name, if any.
        /// </summary>
        /// <param name="columnName">The name of the property to validate.</param>
        /// <returns>An error message if invalid, otherwise an empty string.</returns>
        public string this[string columnName]
        {
            get
            {
                if (!IsManaged) return string.Empty; // Only validate if the monitor is managed

                string? result = null;
                switch (columnName)
                {
                    case nameof(IdleValue):
                        result = ValidateIdleValue();
                        break;

                    case nameof(IsActiveOnInput):
                    case nameof(IsActiveOnMousePosition):
                    case nameof(IsActiveOnActiveWindow):
                        result = ValidateActiveConditions();
                        break;

                    case nameof(Behavior):
                        result = ValidateBehavior();
                        break;
                }
                return result ?? string.Empty;
            }
        }

        /// <summary>
        /// Validates the idle value property.
        /// </summary>
        /// <returns>An error message if invalid, otherwise null.</returns>
        private string? ValidateIdleValue()
        {
            if (IdleValue == null || IdleValue <= 0)
                return "Idle time value must be a number greater than zero.";
            return null;
        }

        /// <summary>
        /// Validates that at least one active condition is selected.
        /// </summary>
        /// <returns>An error message if invalid, otherwise null.</returns>
        private string? ValidateActiveConditions()
        {
            if (!IsActiveOnInput && !IsActiveOnMousePosition && !IsActiveOnActiveWindow)
                return "At least one 'Consider Active When' option must be selected.";
            return null;
        }

        /// <summary>
        /// Validates the Behavior property to ensure a monitor behavior is selected when managed.
        /// </summary>
        /// <returns>
        /// An error message if the behavior is not selected (None), otherwise null.
        /// </returns>
        private string? ValidateBehavior()
        {
            if (Behavior == MonitorBehaviorType.None)
                return "A monitor behavior must be selected.";
            return null;
        }

        #endregion Validation
    }
}