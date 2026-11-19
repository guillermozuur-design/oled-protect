using Microsoft.Win32;

namespace OLED_Sleeper.Infrastructure.Helpers
{
    // "Start with Windows" via the per-user Run key (no admin rights needed).
    public static class StartupHelper
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // Value name under the Run key. Changing it would orphan the entry existing users already have.
        private const string EntryName = "OLED Space Sleeper";

        public static bool IsRunAtStartupEnabled()
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(EntryName) is not null;
        }

        public static void SetRunAtStartup(bool enable)
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run is null)
                return;

            if (!enable)
            {
                run.DeleteValue(EntryName, throwOnMissingValue: false);
                return;
            }

            // -h: start hidden in the tray when launched at sign-in.
            if (Environment.ProcessPath is { Length: > 0 } exe)
                run.SetValue(EntryName, "\"" + exe + "\" -h");
        }
    }
}
