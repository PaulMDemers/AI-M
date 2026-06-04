using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AIM.Desktop.Wpf.Converters;

public sealed class HorizontalAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value as string, "Right", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
