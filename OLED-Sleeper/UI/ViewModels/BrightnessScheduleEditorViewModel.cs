using OLED_Sleeper.Features.BrightnessSchedule.Services;
using OLED_Sleeper.Features.UserSettings.Models;
using OLED_Sleeper.UI.Commands;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OLED_Sleeper.UI.ViewModels
{
    /// <summary>
    /// The "Helderheid op tijden" card in the Algemeen tab: one schedule for all screens. Every valid change
    /// is saved at once (like the other settings in that tab).
    /// </summary>
    public class BrightnessScheduleEditorViewModel : ViewModelBase
    {
        private readonly GlobalBrightnessSchedule _schedule;
        private bool _enabled;
        private bool _loading;
        private string _status = "";

        public BrightnessScheduleEditorViewModel(GlobalBrightnessSchedule schedule)
        {
            _schedule = schedule;
            AddEntryCommand = new RelayCommand(_ => AddEntry());
            RemoveEntryCommand = new RelayCommand(p =>
            {
                if (p is ScheduleEntryViewModel entry && Entries.Remove(entry)) Changed();
            });

            _loading = true;
            _enabled = schedule.Enabled;
            foreach (var e in schedule.Entries) Entries.Add(new ScheduleEntryViewModel(e, Changed));
            _loading = false;
        }

        public ObservableCollection<ScheduleEntryViewModel> Entries { get; } = new();

        public ICommand AddEntryCommand { get; }

        public ICommand RemoveEntryCommand { get; }

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; OnPropertyChanged(); Changed(); }
        }

        /// <summary>What is wrong (shown in red), or "".</summary>
        public string Error => Validate() ?? "";

        /// <summary>"Opgeslagen ✓" after a change was saved.</summary>
        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        private void AddEntry()
        {
            var last = Entries.LastOrDefault();
            var lastTime = last == null ? null : BrightnessScheduleCalculator.ParseTime(last.Start);
            var start = lastTime?.AddHours(2) ?? new TimeOnly(8, 0);
            Entries.Add(new ScheduleEntryViewModel(
                new BrightnessScheduleEntry { Start = start.ToString("HH:mm"), Brightness = last?.Brightness ?? 70 }, Changed));
            Changed();
        }

        private void Changed()
        {
            if (_loading) return;
            OnPropertyChanged(nameof(Error));
            if (Validate() != null) { Status = ""; return; }
            _schedule.Set(Enabled, Entries.Select(e => e.ToEntry()));
            Status = "Opgeslagen ✓";
        }

        private string? Validate()
        {
            if (!Enabled) return null;
            if (Entries.Count == 0) return "Voeg minstens één tijd toe.";
            if (Entries.Any(e => !e.IsStartValid)) return "Gebruik tijden als 08:00 of 19:30.";
            var times = Entries.Select(e => BrightnessScheduleCalculator.ParseTime(e.Start)).ToList();
            if (times.Distinct().Count() != times.Count) return "Elke tijd mag maar één keer voorkomen.";
            return null;
        }
    }
}
