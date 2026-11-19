using OledProtect.UI.Models;
using System.Globalization;
using System.Windows.Data;

namespace OledProtect.UI.Converters
{
    // Dutch labels for the idle-time unit picker.
    public class TimeUnitToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TimeUnit unit)
                return value?.ToString() ?? string.Empty;

            return unit switch
            {
                TimeUnit.Seconds => "seconden",
                TimeUnit.Minutes => "minuten",
                TimeUnit.Hours => "uren",
                _ => unit.ToString()
            };
        }

        // One-way only.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
