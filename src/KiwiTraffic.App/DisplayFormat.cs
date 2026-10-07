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
    /// Settled against a live account on 2026-10-07 (VEID 2213202, plan
    /// KVMV5-20G-1G-1T-CA-CN2GIA): the KiwiVM panel showed "1 TB" for a quota
    /// the API reports as 2^40 bytes, while dividing by 1000 displayed it as
    /// "1.1 TB". KiwiVM therefore divides by 1024 and labels the result TB.
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
