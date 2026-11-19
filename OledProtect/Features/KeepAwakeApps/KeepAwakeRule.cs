namespace OledProtect.Features.KeepAwakeApps
{
    /// <summary>Where a chosen app keeps the screens awake.</summary>
    public enum KeepAwakeScope
    {
        /// <summary>Only the screen the app's window is on (the other screens follow their own timers).</summary>
        ThisScreen,

        /// <summary>Every screen, as long as the app is open (like an automatic work mode).</summary>
        AllScreens
    }

    /// <summary>
    /// An app that keeps screens awake while it is open: a ready-made one from the list (VLC, OBS, …) or one the
    /// user picked from the running programs. Matched on the process name, without ".exe", ignoring case.
    /// </summary>
    public class KeepAwakeRule
    {
        public string Name { get; set; } = string.Empty;

        public List<string> ProcessNames { get; set; } = new();

        public KeepAwakeScope Scope { get; set; } = KeepAwakeScope.ThisScreen;

        public bool Enabled { get; set; }

        /// <summary>True for the ready-made entries; those can be switched off but not removed.</summary>
        public bool IsPreset { get; set; }

        public bool Matches(string processName) =>
            ProcessNames.Any(p => string.Equals(p, processName, StringComparison.OrdinalIgnoreCase));
    }
}
