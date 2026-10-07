using System.Windows;
using System.Windows.Media;

namespace KiwiTraffic.App.Controls;

/// <summary>
/// A rounded horizontal gauge, 0..100. Written by hand so the ends can be
/// rounded and a small value still renders as a visible sliver - the stock
/// <c>ProgressBar</c> does neither.
/// </summary>
public sealed class BarProgress : FrameworkElement
{
    private const double MinimumVisibleWidth = 3d;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(BarProgress),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarHeightProperty = DependencyProperty.Register(
        nameof(BarHeight),
        typeof(double),
        typeof(BarProgress),
        new FrameworkPropertyMetadata(9d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(BarProgress),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush),
        typeof(Brush),
        typeof(BarProgress),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0..100. Values outside the range are clamped.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 200d : availableSize.Width;

        return new Size(width, BarHeight);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var height = Math.Min(BarHeight, ActualHeight > 0 ? ActualHeight : BarHeight);
        var width = ActualWidth;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = height / 2d;
        drawingContext.DrawRoundedRectangle(TrackBrush, null, new Rect(0, 0, width, height), radius, radius);

        var fraction = Math.Clamp(Value, 0d, 100d) / 100d;
        if (fraction <= 0d)
        {
            return;
        }

        var filled = Math.Max(width * fraction, Math.Min(MinimumVisibleWidth, width));
        var filledRadius = Math.Min(radius, filled / 2d);

        drawingContext.DrawRoundedRectangle(
            ProgressBrush,
            null,
            new Rect(0, 0, filled, height),
            filledRadius,
            filledRadius);
    }
}
