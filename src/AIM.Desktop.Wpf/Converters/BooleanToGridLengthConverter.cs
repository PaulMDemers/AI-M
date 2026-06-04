using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIM.Desktop.Wpf.Converters;

public sealed class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isExpanded = value is bool expanded && expanded;

        if (string.Equals(parameter as string, "Contacts", StringComparison.OrdinalIgnoreCase))
        {
            return isExpanded ? new GridLength(320) : new GridLength(1, GridUnitType.Star);
        }

        return isExpanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
