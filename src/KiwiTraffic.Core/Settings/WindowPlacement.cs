namespace KiwiTraffic.Core.Settings;

/// <summary>
/// Where the widget was last seen, in device-independent pixels - the unit WPF
/// itself uses, so no DPI conversion is needed to restore it.
/// </summary>
public sealed record WindowPlacement
{
    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool IsUsable => Width > 0 && Height > 0;
}

/// <summary>The desktop area available to place a window, in DIPs.</summary>
public readonly record struct DesktopBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;
}

/// <summary>
/// Keeps a restored window reachable.
/// </summary>
/// <remarks>
/// A saved position can point at a monitor that has since been unplugged, or
/// off the edge after a resolution change. Rather than trusting it, the
/// position is clamped so a usable strip of the window always lands inside the
/// current desktop.
/// </remarks>
public static class WindowPlacementPolicy
{
    /// <summary>How much of the window must remain on screen, in DIPs.</summary>
    public const double MinimumVisible = 64d;

    /// <summary>
    /// Clamps <paramref name="placement"/> into <paramref name="desktop"/>,
    /// keeping at least <see cref="MinimumVisible"/> of the window reachable.
    /// A placement that is already fine is returned unchanged.
    /// </summary>
    public static WindowPlacement Clamp(WindowPlacement placement, DesktopBounds desktop)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (!placement.IsUsable || desktop.Width <= 0 || desktop.Height <= 0)
        {
            return placement;
        }

        var visibleX = Math.Min(MinimumVisible, placement.Width);
        var visibleY = Math.Min(MinimumVisible, placement.Height);

        // Horizontally the window may hang off either side, as long as a strip
        // of it stays inside; vertically it must not sit above the desktop,
        // where a title-free widget would be impossible to grab.
        var left = Math.Clamp(
            placement.Left,
            desktop.Left - placement.Width + visibleX,
            desktop.Right - visibleX);

        var top = Math.Clamp(
            placement.Top,
            desktop.Top,
            desktop.Bottom - visibleY);

        return placement with { Left = left, Top = top };
    }

    /// <summary>Bottom-right of the work area, inset by <paramref name="margin"/>.</summary>
    public static WindowPlacement BottomRight(DesktopBounds workArea, double width, double height, double margin = 16d)
    {
        return new WindowPlacement
        {
            Width = width,
            Height = height,
            Left = workArea.Right - width - margin,
            Top = workArea.Bottom - height - margin,
        };
    }
}
