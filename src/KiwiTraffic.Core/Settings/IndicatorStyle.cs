namespace KiwiTraffic.Core.Settings;

/// <summary>
/// How the widget renders the usage figure.
/// </summary>
/// <remarks>
/// <para>
/// The numeric values are <em>persisted</em> in settings.json, so they must not
/// be reordered - treat them as a wire format. They are also the indices the
/// settings window's drop-down uses, which is why the order here matches the
/// order of the labels there.
/// </para>
/// <para>
/// Both styles show the same number; only the drawing differs.
/// </para>
/// </remarks>
public enum IndicatorStyle
{
    /// <summary>Circular gauge with the percentage sitting in the middle.</summary>
    Ring = 0,

    /// <summary>Horizontal progress bar, with the percentage above it.</summary>
    Bar = 1,
}
