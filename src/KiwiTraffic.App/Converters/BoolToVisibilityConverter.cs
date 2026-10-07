using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KiwiTraffic.App.Converters;

/// <summary>
/// <see cref="bool"/> to <see cref="Visibility"/>, optionally inverted.
/// WPF ships a plain boolean-to-visibility converter but no inverse.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;

        if (Invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
