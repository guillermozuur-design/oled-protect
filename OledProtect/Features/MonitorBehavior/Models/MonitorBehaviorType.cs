namespace OledProtect.Features.MonitorBehavior.Models
{
    // What happens to a managed screen once it counts as idle. Stored in the per-monitor settings file,
    // so existing members keep their position; new ones go at the end.
    public enum MonitorBehaviorType
    {
        None,       // leave the screen alone
        Dim,        // lower the hardware brightness over DDC/CI
        Blackout,   // cover it with a black overlay
        Screensaver, // black overlay with the starry sky on top
        Flights      // black overlay with the live flight map on top
    }
}
