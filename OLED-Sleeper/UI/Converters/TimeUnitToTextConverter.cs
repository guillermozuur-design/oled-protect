using OLED_Sleeper.UI.Models;
using System.Globalization;
using System.Windows.Data;

namespace OLED_Sleeper.UI.Converters
{
    /// <summary>Shows a <see cref="TimeUnit"/> in Dutch: seconden, minuten, uren.</summary>
    public class TimeUnitToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
        {
            TimeUnit.Seconds => "seconden",
            TimeUnit.Minutes => "minuten",
            TimeUnit.Hours => "uren",
            _ => value?.ToString() ?? string.Empty
        };

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
