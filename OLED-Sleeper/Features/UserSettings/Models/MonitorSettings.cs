using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.UI.Models;

namespace OLED_Sleeper.Features.UserSettings.Models
{
    /// <summary>
    /// Represents user-configurable settings for a monitor, including idle detection and behavior.
    /// </summary>
    public class MonitorSettings
    {
        /// <summary>
        /// Gets or sets the unique hardware ID for the monitor.
        /// </summary>
        public string HardwareId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this monitor is managed by the application.
        /// </summary>
        public bool IsManaged { get; set; } = false;

        /// <summary>
        /// Gets or sets the behavior to apply when the monitor becomes idle (e.g., dim or blackout).
        /// </summary>
        public MonitorBehaviorType Behavior { get; set; } = MonitorBehaviorType.None;

        /// <summary>
        /// Gets or sets the dimming level to apply when the monitor is dimmed.
        /// </summary>
        public double DimLevel { get; set; } = 15;

        /// <summary>
        /// Gets or sets a value indicating whether a blackout also sets the monitor's hardware brightness to zero.
        /// Absent from a settings file written before this option existed, which leaves it at its default of false.
        /// </summary>
        public bool LowerBrightnessOnBlackout { get; set; } = false;

        /// <summary>
        /// Gets or sets the idle timeout value (unit specified by <see cref="IdleUnit"/>).
        /// </summary>
        public int? IdleValue { get; set; } = 30;

        /// <summary>
        /// Gets or sets the time unit for the idle timeout.
        /// </summary>
        public TimeUnit IdleUnit { get; set; } = TimeUnit.Seconds;

        /// <summary>
        /// Gets or sets a value indicating whether system input (keyboard/mouse) should reset idle state.
        /// </summary>
        public bool IsActiveOnInput { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether mouse position should reset idle state.
        /// </summary>
        public bool IsActiveOnMousePosition { get; set; } = true;

        /// <summary>
        /// When true, a mouse that just sits on this monitor only keeps it awake for
        /// <see cref="MouseStillMinutes"/> without any input; after that the monitor goes idle anyway.
        /// </summary>
        public bool UseMouseStillLimit { get; set; } = false;

        /// <summary>
        /// Minutes without any mouse movement or key press after which a parked mouse no longer keeps the monitor awake.
        /// </summary>
        public int MouseStillMinutes { get; set; } = 10;

        /// <summary>
        /// Gets or sets a value indicating whether the active window should reset idle state.
        /// </summary>
        public bool IsActiveOnActiveWindow { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the brightness schedule is applied to this monitor.
        /// Works independently of <see cref="IsManaged"/>. Absent from older settings files, so it defaults to off.
        /// </summary>
        public bool UseBrightnessSchedule { get; set; } = false;

        /// <summary>
        /// Gets or sets the brightness schedule: from each start time onwards the monitor uses that brightness.
        /// </summary>
        public List<BrightnessScheduleEntry> BrightnessSchedule { get; set; } = new()
        {
            new BrightnessScheduleEntry { Start = "08:00", Brightness = 80 },
            new BrightnessScheduleEntry { Start = "19:00", Brightness = 50 },
            new BrightnessScheduleEntry { Start = "23:00", Brightness = 30 }
        };

        /// <summary>
        /// Gets the idle timeout in milliseconds, based on <see cref="IdleValue"/> and <see cref="IdleUnit"/>.
        /// </summary>
        public int IdleTimeMilliseconds
        {
            get
            {
                if (IdleValue == null) return 0;
                return IdleUnit switch
                {
                    TimeUnit.Minutes => IdleValue.Value * 60 * 1000,
                    TimeUnit.Hours => IdleValue.Value * 60 * 60 * 1000,
                    _ => IdleValue.Value * 1000
                };
            }
        }
    }
}