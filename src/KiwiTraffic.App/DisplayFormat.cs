using KiwiTraffic.Core.Formatting;

namespace KiwiTraffic.App;

/// <summary>
/// Presentation choices that must be made in exactly one place.
/// </summary>
public static class DisplayFormat
{
    /// <summary>
    /// Binary (IEC) prefixes: divide by 1024 and label the result TiB / GiB.
    /// </summary>
    /// <remarks>
    /// Settled against a live account on 2026-10-07. The API reports that
    /// account's quota as 1000 x 1024^3 bytes and the panel calls that "1 TB",
    /// so KiwiVM counts in
    /// binary units and labels them TB - dividing by 1000 instead shows
    /// "1.1 TB", which matches nothing the provider displays.
    /// <para>
    /// We divide the same way so the numbers agree with the panel, but label
    /// them TiB / GiB - writing "GB" for a binary multiple is exactly the
    /// mislabelling this project forbids.
    /// </para>
    /// <para>
    /// The percentage is a ratio and is unaffected either way; only the
    /// absolute amounts change.
    /// </para>
    /// </remarks>
    public static BytePrefixStyle BytePrefix { get; } = BytePrefixStyle.Iec;
}
