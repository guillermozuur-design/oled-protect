using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OLED_Sleeper.UI.Converters
{
    // true shows the element; anything else (false, null, not a bool) collapses it so it takes no space.
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Equals(value, true) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Equals(value, Visibility.Visible);
    }
}
