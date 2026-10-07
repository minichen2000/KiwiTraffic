using KiwiTraffic.Core.Model;

namespace KiwiTraffic.Infrastructure.KiwiVm;

/// <summary>Why a query did not produce a usable reading.</summary>
public enum KiwiVmErrorKind
{
    /// <summary>The call succeeded and produced a reading.</summary>
    None,

    /// <summary>
    /// The API rejected the credentials. Stop automatic retries and send the
    /// user back to the settings window.
    /// </summary>
    Authentication,

    /// <summary>
    /// A non-zero API error code we do not classify further. The provider's
    /// message is surfaced verbatim.
    /// </summary>
    Business,

    /// <summary>
    /// The response could not be trusted: not JSON, missing required fields,
    /// or values outside their valid range. The previous snapshot stays.
    /// </summary>
    InvalidResponse,

    /// <summary>Timeout, DNS failure, TLS failure, proxy failure, HTTP error status.</summary>
    Network,

    /// <summary>The caller cancelled the request.</summary>
    Cancelled,
}

/// <summary>
/// Outcome of one KiwiVM call. Never carries credentials.
/// </summary>
public sealed record KiwiVmQueryResult
{
    /// <summary>
    /// The reading, present only on success. A non-null snapshot is always
    /// fully validated - a failed call never yields one.
    /// </summary>
    public TrafficSnapshot? Snapshot { get; init; }

    public KiwiVmErrorKind ErrorKind { get; init; } = KiwiVmErrorKind.None;

    /// <summary>Message safe to show the user. Never contains the API key.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The raw API error code, when the failure came from the API itself.</summary>
    public long? ApiErrorCode { get; init; }

    public bool IsSuccess => ErrorKind == KiwiVmErrorKind.None && Snapshot is not null;

    public static KiwiVmQueryResult Success(TrafficSnapshot snapshot) => new() { Snapshot = snapshot };

    public static KiwiVmQueryResult Failure(KiwiVmErrorKind kind, string message, long? apiErrorCode = null)
        => new() { ErrorKind = kind, ErrorMessage = message, ApiErrorCode = apiErrorCode };
}
