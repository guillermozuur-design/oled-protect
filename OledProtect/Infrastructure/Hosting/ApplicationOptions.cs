using CommandLine;

namespace OledProtect.Infrastructure.Hosting
{
    // Command-line switches, filled by CommandLineHelper at startup.
    public class ApplicationOptions
    {
        // Used by the Windows autostart entry: skip the settings window and only show the tray icon.
        [Option('h', "hide", HelpText = "Launch minimized to the tray, without opening the settings window.")]
        public bool StartHidden { get; set; }
    }
}
