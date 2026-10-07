namespace KiwiTraffic.Core.Settings;

/// <summary>
/// Colour scheme for the widget and the settings window.
/// </summary>
/// <remarks>
/// The numeric values are persisted in settings.json and are also the indices
/// of the settings drop-down, so they must not be reordered.
/// </remarks>
public enum AppTheme
{
    /// <summary>Dark text on a light card.</summary>
    Light = 0,

    /// <summary>Light text on a dark card.</summary>
    Dark = 1,
}
