using System.Globalization;
using System.Windows.Data;

namespace KiwiTraffic.App.Converters;

/// <summary>
/// Two-way bridge between an integer index on a view model and the
/// <c>IsChecked</c> of one chip in a group.
/// </summary>
/// <remarks>
/// Usage: <c>IsChecked="{Binding StyleIndex, Converter={StaticResource IndexIs}, ConverterParameter=1}"</c>.
/// Unchecking (because a sibling was checked) writes nothing back, so the
/// view model is never told "nothing is selected".
/// </remarks>
public sealed class IndexEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int index && TryGetIndex(parameter, out var target) && index == target;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && TryGetIndex(parameter, out var target)
            ? target
            : Binding.DoNothing;

    private static bool TryGetIndex(object? parameter, out int index)
    {
        switch (parameter)
        {
            case int value:
                index = value;
                return true;

            case string text:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);

            default:
                index = 0;
                return false;
        }
    }
}
