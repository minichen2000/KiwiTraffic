using System.Windows;
using System.Windows.Media;

namespace KiwiTraffic.App.Controls;

/// <summary>
/// A circular gauge. 0..100 sweeps clockwise from twelve o'clock.
/// </summary>
/// <remarks>
/// Written by hand rather than pulled from a charting or UI library: it is
/// about eighty lines, and the plan rules out adding a UI framework for one
/// control.
/// </remarks>
public sealed class RingProgress : FrameworkElement
{
    /// <summary>Below this the arc is drawn as a full circle - a 360 degree arc is degenerate.</summary>
    private const double FullCircleSweep = 359.99;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness),
        typeof(double),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush),
        typeof(Brush),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty = DependencyProperty.Register(
        nameof(ProgressBrush),
        typeof(Brush),
        typeof(RingProgress),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>0..100. Values outside the range are clamped.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
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
        // Prefer a square, but never demand more room than the parent offers.
        var width = double.IsInfinity(availableSize.Width) ? 120d : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 120d : availableSize.Height;
        var side = Math.Min(width, height);

        return new Size(side, side);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);

        var thickness = RingThickness;
        var side = Math.Min(ActualWidth, ActualHeight);

        if (side <= thickness * 2)
        {
            return;
        }

        var centre = new Point(ActualWidth / 2d, ActualHeight / 2d);
        var radius = (side - thickness) / 2d;

        var trackPen = CreatePen(TrackBrush, thickness, PenLineCap.Flat);
        drawingContext.DrawEllipse(null, trackPen, centre, radius, radius);

        var sweep = Math.Clamp(Value, 0d, 100d) / 100d * 360d;
        if (sweep <= 0d)
        {
            return;
        }

        var progressPen = CreatePen(ProgressBrush, thickness, PenLineCap.Round);

        if (sweep >= FullCircleSweep)
        {
            drawingContext.DrawEllipse(null, progressPen, centre, radius, radius);
            return;
        }

        drawingContext.DrawGeometry(null, progressPen, BuildArc(centre, radius, sweep));
    }

    private static Pen CreatePen(Brush brush, double thickness, PenLineCap cap)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = cap,
            EndLineCap = cap,
        };

        pen.Freeze();
        return pen;
    }

    /// <summary>Builds the arc starting at twelve o'clock and going clockwise.</summary>
    private static StreamGeometry BuildArc(Point centre, double radius, double sweepDegrees)
    {
        var start = PointOnCircle(centre, radius, 0d);
        var end = PointOnCircle(centre, radius, sweepDegrees);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, isFilled: false, isClosed: false);
            context.ArcTo(
                end,
                new Size(radius, radius),
                rotationAngle: 0d,
                isLargeArc: sweepDegrees > 180d,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Angle in degrees, 0 at twelve o'clock, increasing clockwise.</summary>
    private static Point PointOnCircle(Point centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180d;

        return new Point(
            centre.X + (radius * Math.Sin(radians)),
            centre.Y - (radius * Math.Cos(radians)));
    }
}
