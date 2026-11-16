namespace OLED_Sleeper.Features.UserSettings.Models
{
    /// <summary>
    /// One step of a brightness schedule: from <see cref="Start"/> (HH:mm) onwards the monitor uses
    /// <see cref="Brightness"/> percent, until the next entry starts. The last entry of the day wraps
    /// around past midnight.
    /// </summary>
    public class BrightnessScheduleEntry
    {
        public string Start { get; set; } = "08:00";

        public int Brightness { get; set; } = 70;
    }
}
