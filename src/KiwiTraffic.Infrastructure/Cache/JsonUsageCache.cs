using System.Text.Json;
using KiwiTraffic.Core.Cache;
using KiwiTraffic.Infrastructure.Storage;

namespace KiwiTraffic.Infrastructure.Cache;

/// <summary>Stores the last valid reading as JSON in the application data directory.</summary>
public sealed class JsonUsageCache : IUsageCache
{
    private readonly AppPaths _paths;

    public JsonUsageCache(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public async Task<CachedUsage?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = _paths.CacheFile;
        var text = await AtomicFile.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<CachedUsage>(text, JsonStoreFormat.Options);

            // A cache written by a newer schema, or one that lost its profile
            // binding, is not trustworthy - discard rather than guess.
            if (cached is null ||
                cached.SchemaVersion > CachedUsage.CurrentSchemaVersion ||
                string.IsNullOrEmpty(cached.ProfileId))
            {
                AtomicFile.MoveToBackup(path);
                return null;
            }

            return cached;
        }
        catch (JsonException)
        {
            AtomicFile.MoveToBackup(path);
            return null;
        }
    }

    public async Task SaveAsync(CachedUsage entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var json = JsonSerializer.Serialize(entry, JsonStoreFormat.Options);
        await AtomicFile.WriteAsync(_paths.CacheFile, json, cancellationToken).ConfigureAwait(false);
    }
}
