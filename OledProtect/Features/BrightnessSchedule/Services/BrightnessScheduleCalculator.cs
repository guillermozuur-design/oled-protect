using OledProtect.Features.UserSettings.Models;
using System.Globalization;

namespace OledProtect.Features.BrightnessSchedule.Services
{
    /// <summary>
    /// Works out which brightness a schedule asks for at a given time of day.
    /// </summary>
    public static class BrightnessScheduleCalculator
    {
        /// <summary>
        /// Parses "HH:mm" (or "H:mm"). Returns null for anything else.
        /// </summary>
        public static TimeOnly? ParseTime(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return TimeOnly.TryParseExact(text.Trim(), new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time
                : null;
        }

        /// <summary>
        /// The brightness (0-100) in force at <paramref name="now"/>, or null when the schedule has no
        /// valid entries. The entry with the latest start at or before <paramref name="now"/> wins; before
        /// the first entry of the day, the last entry of the previous day is still in force.
        /// </summary>
        public static int? GetTargetBrightness(IEnumerable<BrightnessScheduleEntry>? entries, TimeOnly now)
        {
            if (entries == null) return null;

            var valid = entries
                .Select(e => (Time: ParseTime(e.Start), e.Brightness))
                .Where(e => e.Time.HasValue)
                .Select(e => (Time: e.Time!.Value, Brightness: Math.Clamp(e.Brightness, 0, 100)))
                .OrderBy(e => e.Time)
                .ToList();

            if (valid.Count == 0) return null;

            var current = valid.LastOrDefault(e => e.Time <= now);
            return current == default ? valid[^1].Brightness : current.Brightness;
        }
    }
}
