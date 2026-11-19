using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OledProtect.UI.Converters
{
    // Hides a text element (such as a validation message) while there is no text to show.
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is string { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        // One-way only.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
