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

    public string? ApiKey { get; init; }

    /// <summary>
    /// Proxy password, if the user chose a manual proxy with authentication.
    /// Never written into the proxy URL or into settings.json.
    /// </summary>
    public string? ProxyPassword { get; init; }

    public bool IsEmpty =>
        string.IsNullOrEmpty(ApiKey) && string.IsNullOrEmpty(ProxyPassword);
}
