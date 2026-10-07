using System.Globalization;

namespace KiwiTraffic.Core.Formatting;

/// <summary>Which family of unit prefixes to use when rendering a byte count.</summary>
public enum BytePrefixStyle
{
    /// <summary>SI prefixes: powers of 1000, labelled B / KB / MB / GB / TB.</summary>
    Si,

    /// <summary>IEC prefixes: powers of 1024, labelled B / KiB / MiB / GiB / TiB.</summary>
    Iec,
}

/// <summary>
/// Renders byte counts with an explicit unit.
/// </summary>
/// <remarks>
/// The prefix family is a required argument rather than a default so that no
/// call site can silently label binary units as decimal ones. Which family
/// matches the KiwiVM panel is an open contract question (see
/// <c>docs/api-contract.md</c>) - it must be decided once, in the view model,
/// not guessed per format call.
/// </remarks>
public static class ByteSizeFormatter
{
    private static readonly string[] SiUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
    private static readonly string[] IecUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

    /// <summary>
    /// Formats <paramref name="bytes"/> using the largest unit that keeps the
    /// value at or above 1, with a fixed number of fraction digits.
    /// </summary>
    /// <param name="bytes">A non-negative byte count.</param>
    /// <param name="style">SI (1000) or IEC (1024) prefixes.</param>
    /// <param name="decimals">Fraction digits for every unit except plain bytes.</param>
    public static string Format(decimal bytes, BytePrefixStyle style, int decimals = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, 6);

        var step = style == BytePrefixStyle.Iec ? 1024m : 1000m;
        var units = style == BytePrefixStyle.Iec ? IecUnits : SiUnits;

        var index = 0;
        var value = bytes;
        while (value >= step && index < units.Length - 1)
        {
            value /= step;
            index++;
        }

        var number = index == 0
            ? value.ToString("0", CultureInfo.InvariantCulture)
            : value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        return $"{number} {units[index]}";
    }
}
