using KiwiTraffic.Core.Cache;

namespace KiwiTraffic.Infrastructure.Cache;

/// <summary>
/// Stores the last valid reading so the widget can show something before the
/// first successful fetch of a session.
/// </summary>
public interface IUsageCache
{
    /// <summary>
    /// The cached reading, or <c>null</c> when there is none.
    /// </summary>
    /// <remarks>
    /// A cache is disposable data: an unreadable one is moved aside and
    /// reported as "no cache", never surfaced as an error to the user.
    /// </remarks>
    Task<CachedUsage?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CachedUsage entry, CancellationToken cancellationToken = default);
}
