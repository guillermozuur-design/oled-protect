using OLED_Sleeper.UI.ViewModels;
using System.Text;

namespace OLED_Sleeper.UI.Helpers
{
    // Decides whether the edited monitor settings may be written, and explains to the user why not.
    // Monitors that are not managed are ignored: their half-filled fields do no harm.
    public static class MonitorSettingsValidator
    {
        private const string Intro = "Een of meer schermen zijn nog niet goed ingesteld en kunnen niet worden opgeslagen:";
        private const string Advice = "Pas de gemarkeerde velden aan, of zet het bewaken van dat scherm uit.";

        // Null when everything can be saved; otherwise a ready-to-show message naming each problem.
        public static string? BuildValidationError(IEnumerable<MonitorLayoutViewModel> monitors)
        {
            StringBuilder? report = null;

            foreach (var monitor in monitors)
            {
                var cfg = monitor.Configuration;
                if (!cfg.IsManaged || cfg.IsValid)
                    continue;

                report ??= new StringBuilder().AppendLine(Intro);
                report.Append("• ").Append(monitor.MonitorTitle).AppendLine(":");

                foreach (var problem in new[] { cfg.BehaviorError, cfg.IdleValueError, cfg.ActiveConditionsError })
                {
                    if (!string.IsNullOrWhiteSpace(problem))
                        report.Append("    – ").AppendLine(problem);
                }
            }

            return report?.AppendLine().AppendLine(Advice).ToString();
        }
    }
}
