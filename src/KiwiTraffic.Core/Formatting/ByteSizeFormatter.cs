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
    /// <summary>
    /// The value at which the next unit is used <em>regardless of the divisor</em>.
    /// </summary>
    /// <remarks>
    /// Binary prefixes divide by 1024 but step up at 1000. That is deliberate:
    /// it avoids readings like "1023.9 MiB", and it keeps us in step with
    /// provider panels - KiwiVM reports a quota of 1000 GiB and displays it as
    /// "1 TB", so dividing by 1024 alone would show "1000.0 GiB" for the same
    /// thing. The label stays binary (TiB, not TB); only the step point moves.
    /// </remarks>
    private const decimal SwitchThreshold = 1000m;

    private static readonly string[] SiUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];
    private static readonly string[] IecUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

    /// <summary>
    /// Formats <paramref name="bytes"/> using the largest unit that keeps the
    /// value at or above <see cref="SwitchThreshold"/>, with a fixed number of
    /// fraction digits.
    /// </summary>
    /// <param name="bytes">A non-negative byte count.</param>
    /// <param name="style">SI (divide by 1000) or IEC (divide by 1024) prefixes.</param>
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
        while (value >= SwitchThreshold && index < units.Length - 1)
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
