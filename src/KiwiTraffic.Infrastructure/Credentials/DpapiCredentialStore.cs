using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Infrastructure.Storage;

namespace KiwiTraffic.Infrastructure.Credentials;

/// <summary>
/// Encrypts the credential file with Windows DPAPI in <see cref="DataProtectionScope.CurrentUser"/>.
/// </summary>
/// <remarks>
/// The plaintext JSON exists only in memory. The extra entropy ties the blob to
/// this application, so another program running as the same user cannot simply
/// call <c>Unprotect</c> on it and get the API key.
/// </remarks>
public sealed class DpapiCredentialStore : ICredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KiwiTraffic/credentials/v1");

    private readonly AppPaths _paths;

    public DpapiCredentialStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public async Task<CredentialLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var blob = await AtomicFile.ReadAllBytesAsync(_paths.CredentialsFile, cancellationToken).ConfigureAwait(false);
        if (blob is null || blob.Length == 0)
        {
            return CredentialLoadResult.Missing;
        }

        byte[] plaintext;
        try
        {
            plaintext = ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            // Copied from another machine, or a different Windows user.
            return CredentialLoadResult.Undecryptable;
        }

        try
        {
            var credentials = JsonSerializer.Deserialize<StoredCredentials>(
                Encoding.UTF8.GetString(plaintext),
                JsonStoreFormat.Options);

            return credentials is null || credentials.IsEmpty
                ? CredentialLoadResult.Missing
                : CredentialLoadResult.Loaded(credentials);
        }
        catch (JsonException)
        {
            return CredentialLoadResult.Undecryptable;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task SaveAsync(StoredCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(credentials, JsonStoreFormat.Options));
        try
        {
            var blob = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
            await AtomicFile.WriteAsync(_paths.CredentialsFile, blob, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = _paths.CredentialsFile;
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
