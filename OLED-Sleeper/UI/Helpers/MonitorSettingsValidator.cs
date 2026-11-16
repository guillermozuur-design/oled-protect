using OLED_Sleeper.UI.ViewModels;
using System.Text;

namespace OLED_Sleeper.UI.Helpers
{
    /// <summary>
    /// Provides validation logic for monitor settings.
    /// This is a static helper class and does not hold any state.
    /// </summary>
    public static class MonitorSettingsValidator
    {
        /// <summary>
        /// Validates the provided monitor settings.
        /// </summary>
        /// <param name="monitors">The collection of monitor layout view models to validate.</param>
        /// <returns>An error message describing every invalid monitor, or null when all are valid.</returns>
        public static string? BuildValidationError(IEnumerable<MonitorLayoutViewModel> monitors)
        {
            var invalidMonitors = GetInvalidMonitors(monitors);

            return invalidMonitors.Count > 0 ? BuildMessage(invalidMonitors) : null;
        }

        /// <summary>
        /// Returns a list of monitors that are managed but have invalid configuration.
        /// </summary>
        /// <param name="monitors">The collection of monitor layout view models to check.</param>
        /// <returns>A list of invalid monitor view models.</returns>
        private static List<MonitorLayoutViewModel> GetInvalidMonitors(IEnumerable<MonitorLayoutViewModel> monitors)
        {
            return monitors
                .Where(m => m.Configuration.IsManaged && !m.Configuration.IsValid)
                .ToList();
        }

        /// <summary>
        /// Builds the validation error message for the provided invalid monitors.
        /// </summary>
        /// <param name="invalidMonitors">The list of invalid monitor view models.</param>
        /// <returns>The assembled error message.</returns>
        private static string BuildMessage(List<MonitorLayoutViewModel> invalidMonitors)
        {
            var errorBuilder = new StringBuilder();
            errorBuilder.AppendLine("Een of meer schermen zijn nog niet goed ingesteld en kunnen niet worden opgeslagen:");
            foreach (var monitor in invalidMonitors)
            {
                errorBuilder.AppendLine($" - {monitor.MonitorTitle}:");
                var config = monitor.Configuration;
                if (!string.IsNullOrWhiteSpace(config.BehaviorError))
                    errorBuilder.AppendLine($"     • {config.BehaviorError}");
                if (!string.IsNullOrWhiteSpace(config.IdleValueError))
                    errorBuilder.AppendLine($"     • {config.IdleValueError}");
                if (!string.IsNullOrWhiteSpace(config.ActiveConditionsError))
                    errorBuilder.AppendLine($"     • {config.ActiveConditionsError}");
            }
            errorBuilder.AppendLine("\nPas de gemarkeerde velden aan, of zet het bewaken van dat scherm uit.");

            return errorBuilder.ToString();
        }
    }
}
