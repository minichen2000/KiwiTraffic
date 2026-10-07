using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.App.Converters;

/// <summary>
/// Maps a <see cref="UsageLevel"/> to the gauge accent colour.
/// </summary>
/// <remarks>
/// Colour is decoration: the percentage and the wording carry the same
/// information, so nothing here is load-bearing.
/// </remarks>
public sealed class UsageLevelToBrushConverter : IValueConverter
{
    public Brush Normal { get; set; } = new SolidColorBrush(Color.FromRgb(0x2D, 0x7F, 0xF9));

    public Brush Warning { get; set; } = new SolidColorBrush(Color.FromRgb(0xE0, 0x9A, 0x2B));

    public Brush Critical { get; set; } = new SolidColorBrush(Color.FromRgb(0xE0, 0x45, 0x4A));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        UsageLevel.Warning => Warning,
        UsageLevel.Critical => Critical,
        _ => Normal,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
