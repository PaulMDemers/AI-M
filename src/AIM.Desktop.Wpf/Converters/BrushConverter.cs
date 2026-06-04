using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AIM.Desktop.Wpf.Converters;

public sealed class BrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.BrushConverter Converter = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string color && !string.IsNullOrWhiteSpace(color)
            ? (SolidColorBrush)Converter.ConvertFromString(color)!
            : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
