using KiwiTraffic.Core.Model;

namespace KiwiTraffic.Core.Cache;

/// <summary>
/// The last valid reading, persisted so the widget can show something before
/// (or without) a successful fetch.
/// </summary>
public sealed record CachedUsage
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// The configuration this reading belongs to. A cache entry whose profile
    /// does not match the current settings must be ignored, never displayed.
    /// </summary>
    public required string ProfileId { get; init; }

    public required TrafficSnapshot Snapshot { get; init; }

    /// <summary>True when this entry may be shown for <paramref name="settings"/>.</summary>
    public bool BelongsTo(Settings.AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return string.Equals(ProfileId, settings.ProfileId, StringComparison.Ordinal);
    }
}
