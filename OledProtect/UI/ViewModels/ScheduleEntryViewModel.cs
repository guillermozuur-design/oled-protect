using OledProtect.Features.BrightnessSchedule.Services;
using OledProtect.Features.UserSettings.Models;

namespace OledProtect.UI.ViewModels
{
    /// <summary>
    /// One editable row of a monitor's brightness schedule.
    /// </summary>
    public class ScheduleEntryViewModel : ViewModelBase
    {
        private readonly Action _onChanged;
        private string _start;
        private int _brightness;

        public ScheduleEntryViewModel(BrightnessScheduleEntry entry, Action onChanged)
        {
            _start = entry.Start;
            _brightness = entry.Brightness;
            _onChanged = onChanged;
        }

        public string Start
        {
            get => _start;
            set { _start = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsStartValid)); _onChanged(); }
        }

        public int Brightness
        {
            get => _brightness;
            set { _brightness = Math.Clamp(value, 0, 100); OnPropertyChanged(); _onChanged(); }
        }

        public bool IsStartValid => BrightnessScheduleCalculator.ParseTime(Start) != null;

        public BrightnessScheduleEntry ToEntry() => new() { Start = Start.Trim(), Brightness = Brightness };
    }
}
