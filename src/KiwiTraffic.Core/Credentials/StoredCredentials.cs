using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Credentials;

/// <summary>
/// Secrets kept on this machine. Serialized to JSON and then encrypted with
/// DPAPI (CurrentUser) before it ever touches the disk, so the plaintext form
/// exists only in memory.
/// </summary>
public sealed record StoredCredentials
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// The configuration <see cref="ApiKey"/> was entered for. Without this, a
    /// key could survive a switch to a different VPS and be sent to the wrong
    /// account - which the API would answer with a confusing auth failure
    /// rather than an obvious "wrong machine".
    /// </summary>
    public string? ApiKeyProfileId { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>
    /// Proxy password, if the user chose a manual proxy with authentication.
    /// Machine-wide, so deliberately not tied to a VPS. Never written into the
    /// proxy URL or into settings.json.
    /// </summary>
    public string? ProxyPassword { get; init; }

    public bool IsEmpty =>
        string.IsNullOrEmpty(ApiKey) && string.IsNullOrEmpty(ProxyPassword);

    /// <summary>
    /// True when the stored key may be used for <paramref name="settings"/>.
    /// A key stored for a different VPS counts as "no key at all": the user is
    /// asked again instead of silently querying the wrong account.
    /// </summary>
    public bool ApiKeyBelongsTo(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return !string.IsNullOrEmpty(ApiKey)
            && string.Equals(ApiKeyProfileId, settings.ProfileId, StringComparison.Ordinal);
    }
}
