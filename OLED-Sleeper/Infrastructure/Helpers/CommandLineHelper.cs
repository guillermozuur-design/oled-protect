using CommandLine;
using OLED_Sleeper.Infrastructure.Hosting;

namespace OLED_Sleeper.Infrastructure.Helpers
{
    public static class CommandLineHelper
    {
        // Bad or unknown arguments never stop the app; it just starts with default options.
        public static ApplicationOptions ParseArguments(string[] args) =>
            Parser.Default
                .ParseArguments<ApplicationOptions>(args)
                .MapResult(parsed => parsed, _ => new ApplicationOptions());
    }
}
