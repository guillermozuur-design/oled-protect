namespace OLED_Sleeper.Features.MonitorBehavior.Models
{
    /// <summary>
    /// Specifies the behavior to apply to a monitor when it becomes idle.
    /// </summary>
    public enum MonitorBehaviorType
    {
        /// <summary>
        /// No behavior is set.
        /// </summary>
        None,

        /// <summary>
        /// Dim the monitor's brightness.
        /// </summary>
        Dim,

        /// <summary>
        /// Apply a blackout overlay to the monitor.
        /// </summary>
        Blackout,

        /// <summary>
        /// Show the OLED-friendly starfield screensaver on the monitor.
        /// </summary>
        Screensaver,

        /// <summary>
        /// Show the flights screensaver: a dim, drifting map with the real aircraft flying over it.
        /// </summary>
        Flights
    }
}