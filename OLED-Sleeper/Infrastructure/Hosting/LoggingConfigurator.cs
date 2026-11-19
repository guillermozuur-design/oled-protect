using Serilog;
using Serilog.Events;
using System.IO;

namespace OLED_Sleeper.Infrastructure.Hosting
{
    // Serilog to daily files in <storage root>\Logs, keeping about a week of history.
    public static class LoggingConfigurator
    {
        private static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

        // storageDirectory must already exist and be writable: the file sink fails silently otherwise.
        public static void Configure(string storageDirectory)
        {
            string folder = Path.Combine(storageDirectory, "Logs");

            var setup = new LoggerConfiguration();
            setup.MinimumLevel.Is(LogEventLevel.Debug);
            setup.WriteTo.File(Path.Combine(folder, "log-.txt"), rollingInterval: RollingInterval.Day);
            Log.Logger = setup.CreateLogger();

            Log.Information("=== OLED Space Sleeper started ===");
            Log.Information("Data folder: {StorageDirectory}", storageDirectory);

            PruneOldLogs(folder);
        }

        // Housekeeping only; a locked or vanished file must never break startup.
        private static void PruneOldLogs(string folder)
        {
            if (!Directory.Exists(folder))
                return;

            DateTime cutoff = DateTime.UtcNow - KeepFor;
            IEnumerable<FileInfo> candidates;
            try
            {
                candidates = new DirectoryInfo(folder).EnumerateFiles("log-*.txt").ToList();
            }
            catch (Exception)
            {
                return;
            }

            foreach (var file in candidates.Where(f => f.CreationTimeUtc < cutoff))
            {
                try { file.Delete(); }
                catch (Exception) { /* try again next start */ }
            }
        }
    }
}
