using OLED_Sleeper.Features.MonitorBehavior.Models;
using OLED_Sleeper.UI.Models;

namespace OLED_Sleeper.Features.UserSettings.Models
{
    // Per-monitor configuration as stored in settings.json. Property names are the JSON keys of existing
    // files, so they must not be renamed. Keys missing from older files simply keep the defaults below.
    public class MonitorSettings
    {
        // Stable identity of the physical monitor (survives reconnects and port changes).
        public string HardwareId { get; set; } = "";

        // Whether idle handling is on for this monitor at all.
        public bool IsManaged { get; set; }

        public MonitorBehaviorType Behavior { get; set; }

        // How far the dim behaviour lowers this screen.
        public double DimLevel { get; set; } = 15;

        // Blackout also drives the hardware backlight to 0 via DDC/CI.
        public bool LowerBrightnessOnBlackout { get; set; }

        // Timeout amount, interpreted in IdleUnit. Null while the user has cleared the field.
        public int? IdleValue { get; set; } = 30;

        public TimeUnit IdleUnit { get; set; }

        // Any keyboard or mouse input anywhere keeps this monitor awake.
        public bool IsActiveOnInput { get; set; }

        // The cursor resting on this monitor keeps it awake.
        public bool IsActiveOnMousePosition { get; set; } = true;

        // Caps the cursor rule above: after MouseStillMinutes without input a parked cursor stops counting.
        public bool UseMouseStillLimit { get; set; }

        public int MouseStillMinutes { get; set; } = 10;

        // The foreground window sitting on this monitor keeps it awake.
        public bool IsActiveOnActiveWindow { get; set; }

        // Independent of IsManaged: a monitor can follow the schedule without idle handling.
        public bool UseBrightnessSchedule { get; set; }

        public List<BrightnessScheduleEntry> BrightnessSchedule { get; set; } = DefaultSchedule();

        // Derived; serialized along with the rest but never read back.
        public int IdleTimeMilliseconds => IdleValue is int amount ? amount * MillisecondsPer(IdleUnit) : 0;

        private static int MillisecondsPer(TimeUnit unit) => unit switch
        {
            TimeUnit.Hours => 3_600_000,
            TimeUnit.Minutes => 60_000,
            _ => 1_000,
        };

        private static List<BrightnessScheduleEntry> DefaultSchedule() =>
        [
            new() { Start = "08:00", Brightness = 80 },
            new() { Start = "19:00", Brightness = 50 },
            new() { Start = "23:00", Brightness = 30 },
        ];
    }
}
