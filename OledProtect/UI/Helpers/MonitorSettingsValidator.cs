using OledProtect.UI.ViewModels;

namespace OledProtect.UI.Helpers
{
    /// <summary>
    /// Checks the edited screens before saving. A screen that is switched off is never judged:
    /// whatever is half filled in there does no harm.
    /// </summary>
    public static class MonitorSettingsValidator
    {
        private const string Opening = "Bijna klaar! Bij deze schermen mist nog iets:";
        private const string Closing = "Vul het aan, of zet de schakelaar van zo'n scherm uit als het geen bewaking hoeft.";

        /// <summary>
        /// Null when all screens can be saved. Otherwise a short message: one paragraph per screen
        /// with what is missing there, between an opening and a closing line.
        /// </summary>
        public static string? BuildValidationError(IEnumerable<MonitorLayoutViewModel> monitors)
        {
            var paragraphs = monitors
                .Where(screen => screen.Configuration is { IsManaged: true, IsValid: false })
                .Select(Describe)
                .ToList();

            if (paragraphs.Count == 0)
                return null;

            var blankLine = Environment.NewLine + Environment.NewLine;
            return Opening + blankLine + string.Join(blankLine, paragraphs) + blankLine + Closing;
        }

        // "Scherm 2 → first problem. Second problem." in the order the panel shows the blocks.
        private static string Describe(MonitorLayoutViewModel screen)
        {
            var c = screen.Configuration;
            var missing = new[] { c.BehaviorError, c.IdleValueError, c.ActiveConditionsError }
                .Where(text => !string.IsNullOrWhiteSpace(text));
            return $"{screen.MonitorTitle} → {string.Join(" ", missing)}";
        }
    }
}
