using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OledProtect.UI.Converters
{
    // Positions an element inside a Grid by turning bound (x, y) coordinates into a left/top margin.
    public class LeftTopToMarginConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Bindings that are not ready yet (UnsetValue) put the element at the origin.
            if (values is [double x, double y])
                return new Thickness(x, y, 0, 0);

            return new Thickness();
        }

        // One-way only.
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
