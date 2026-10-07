using KiwiTraffic.Core.Credentials;

namespace KiwiTraffic.Infrastructure.Credentials;

/// <summary>Outcome of trying to read the stored secrets.</summary>
public enum CredentialLoadStatus
{
    /// <summary>Nothing has been stored yet.</summary>
    Missing,

    /// <summary>Credentials were read successfully.</summary>
    Loaded,

    /// <summary>
    /// The file exists but cannot be decrypted on this machine or for this
    /// Windows user (DPAPI CurrentUser keys are not portable). The user has to
    /// re-enter the API key.
    /// </summary>
    Undecryptable,
}

/// <summary>Result of <see cref="ICredentialStore.LoadAsync"/>.</summary>
public sealed record CredentialLoadResult(CredentialLoadStatus Status, StoredCredentials? Credentials)
{
    public static CredentialLoadResult Missing { get; } = new(CredentialLoadStatus.Missing, null);

    public static CredentialLoadResult Undecryptable { get; } = new(CredentialLoadStatus.Undecryptable, null);

    public static CredentialLoadResult Loaded(StoredCredentials credentials) =>
        new(CredentialLoadStatus.Loaded, credentials);
}

/// <summary>
/// Stores the API key and proxy password. Implementations must encrypt at rest
/// and must never expose the plaintext through logs, exceptions or URLs.
/// </summary>
public interface ICredentialStore
{
    Task<CredentialLoadResult> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(StoredCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Removes stored secrets. Succeeds when there is nothing to remove.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
}
