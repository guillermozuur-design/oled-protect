namespace OLED_Sleeper.Features.UserSettings.Models
{
    // A step in a brightness schedule. It holds from Start ("HH:mm") until the next entry begins;
    // the latest entry of the day carries on past midnight into the next morning.
    public class BrightnessScheduleEntry
    {
        public string Start { get; set; } = "08:00";

        // Percent, 0-100.
        public int Brightness { get; set; } = 70;
    }
}
