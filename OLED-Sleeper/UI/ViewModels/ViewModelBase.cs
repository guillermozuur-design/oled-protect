using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OLED_Sleeper.UI.ViewModels
{
    /// <summary>
    /// Shared change-notification plumbing for every view model in the app.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Tells bindings that <paramref name="propertyName"/> may have a new value.</summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new(propertyName));

        /// <summary>
        /// Stores <paramref name="value"/> and notifies, but only when it actually differs, so callers
        /// can hang follow-up work (dirty tracking, dependent properties) on the return value.
        /// </summary>
        protected bool SetField<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
