using System.Globalization;
using System.Windows.Data;

namespace OledProtect.UI.Converters
{
    // Lets a group of RadioButtons edit one enum property: each button passes "its" member as the
    // ConverterParameter and is checked when the bound value has the same name.
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            SameName(value, parameter);

        // Only the button that became checked writes back; the one being unchecked leaves the value alone.
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool nowChecked = value as bool? ?? false;
            if (!nowChecked || parameter is null)
                return Binding.DoNothing;

            string member = parameter.ToString() ?? "";
            return Enum.Parse(targetType, member);
        }

        private static bool SameName(object? a, object? b) =>
            a is not null && b is not null &&
            string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
