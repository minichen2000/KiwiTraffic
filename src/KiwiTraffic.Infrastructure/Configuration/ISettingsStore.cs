using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Infrastructure.Configuration;

/// <summary>
/// Persists <see cref="AppSettings"/>. Implementations never store secrets.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Loads the settings, or <c>null</c> when nothing has been configured yet.
    /// </summary>
    /// <exception cref="Storage.CorruptStoreException">
    /// The file exists but cannot be used. The original has been backed up;
    /// the caller must tell the user rather than silently starting from defaults.
    /// </exception>
    Task<AppSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
