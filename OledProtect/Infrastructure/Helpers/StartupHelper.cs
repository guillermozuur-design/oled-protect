using Microsoft.Win32;

namespace OledProtect.Infrastructure.Helpers
{
    // "Start with Windows" via the per-user Run key (no admin rights needed).
    public static class StartupHelper
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        // Value name under the Run key.
        private const string EntryName = "OLED Protect";

        // Until version 3 the app was called "OLED Space Sleeper"; its entry is taken over once.
        private const string FormerEntryName = "OLED Space Sleeper";

        public static bool IsRunAtStartupEnabled()
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            return run?.GetValue(EntryName) is not null;
        }

        // Called once at start by the installed build: replaces an autostart entry left by the former name.
        public static void TakeOverFormerEntry()
        {
#if PORTABLE
            return;
#else
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (run?.GetValue(FormerEntryName) is null)
                    return;
                run.DeleteValue(FormerEntryName, throwOnMissingValue: false);
                if (run.GetValue(EntryName) is null && Environment.ProcessPath is { Length: > 0 } exe)
                    run.SetValue(EntryName, "\"" + exe + "\" -h");
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
            {
                // Not allowed right now: the old entry simply stays until the next start.
            }
#endif
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
