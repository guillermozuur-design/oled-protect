using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OLED_Sleeper.UI.Converters
{
    // Shows an element only while the bound value is null (a "nothing selected" placeholder).
    // With IsReversed it is the other way round: shown only while there is a value.
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool IsReversed { get; set; }

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            bool show = (value is null) != IsReversed;
            return show ? Visibility.Visible : Visibility.Collapsed;
        }

        // One-way only.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
