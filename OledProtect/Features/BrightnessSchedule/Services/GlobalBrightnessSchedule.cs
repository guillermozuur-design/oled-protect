using OledProtect.Features.UserSettings.Models;
using OledProtect.Features.UserSettings.Services.Interfaces;
using OledProtect.Storage.Interfaces;

namespace OledProtect.Features.BrightnessSchedule.Services
{
    /// <summary>
    /// One brightness schedule for all screens ("Helderheid op tijden" in the Algemeen tab), remembered in
    /// <c>brightness-schedule.json</c>. The first time, the schedule a screen already had is taken over.
    /// </summary>
    public class GlobalBrightnessSchedule
    {
        private const string FileName = "brightness-schedule.json";

        public sealed class Stored
        {
            public bool Enabled { get; set; }
            public List<BrightnessScheduleEntry> Entries { get; set; } = new();
        }

        private readonly IAppDataFileStore? _fileStore;
        private readonly object _lock = new();
        private Stored _value;

        public GlobalBrightnessSchedule(IAppDataFileStore? fileStore = null, IMonitorSettingsFileService? monitorSettings = null)
        {
            _fileStore = fileStore;
            var stored = _fileStore?.Read<Stored>(FileName);
            if (stored == null)
            {
                // Before, the schedule was set per screen: take over the first one that was on.
                stored = FromScreens(monitorSettings?.LoadSettings());
                if (monitorSettings != null) _fileStore?.TryWrite(FileName, stored);
            }
            _value = Sanitize(stored);
        }

        /// <summary>What the per-screen settings had: the first schedule that was switched on, else the default one, off.</summary>
        public static Stored FromScreens(IEnumerable<MonitorSettings>? screens)
        {
            var on = screens?.FirstOrDefault(s => s.UseBrightnessSchedule && s.BrightnessSchedule?.Count > 0);
            return new Stored
            {
                Enabled = on != null,
                Entries = (on?.BrightnessSchedule ?? new MonitorSettings().BrightnessSchedule)
                          .Select(e => new BrightnessScheduleEntry { Start = e.Start, Brightness = e.Brightness }).ToList()
            };
        }

        /// <summary>Fires when the schedule was changed.</summary>
        public event Action? Changed;

        public bool Enabled { get { lock (_lock) return _value.Enabled; } }

        /// <summary>A copy of the schedule's steps.</summary>
        public List<BrightnessScheduleEntry> Entries
        {
            get
            {
                lock (_lock) return _value.Entries.Select(e => new BrightnessScheduleEntry { Start = e.Start, Brightness = e.Brightness }).ToList();
            }
        }

        /// <summary>The brightness the schedule asks for now (null when it is off or empty).</summary>
        public int? TargetAt(TimeOnly now)
        {
            lock (_lock) return _value.Enabled ? BrightnessScheduleCalculator.GetTargetBrightness(_value.Entries, now) : null;
        }

        public void Set(bool enabled, IEnumerable<BrightnessScheduleEntry> entries)
        {
            var next = Sanitize(new Stored { Enabled = enabled, Entries = entries.ToList() });
            lock (_lock)
            {
                if (next.Enabled == _value.Enabled && Signature(next) == Signature(_value)) return;
                _value = next;
            }
            _fileStore?.TryWrite(FileName, next);
            Changed?.Invoke();
        }

        private static string Signature(Stored s) => string.Join(";", s.Entries.Select(e => $"{e.Start}={e.Brightness}"));

        private static Stored Sanitize(Stored s) => new()
        {
            Enabled = s.Enabled,
            Entries = (s.Entries ?? new())
                .Where(e => e != null && BrightnessScheduleCalculator.ParseTime(e.Start) != null)
                .Select(e => new BrightnessScheduleEntry { Start = e.Start.Trim(), Brightness = Math.Clamp(e.Brightness, 0, 100) })
                .ToList()
        };
    }
}
