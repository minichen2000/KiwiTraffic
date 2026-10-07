using System.Text.Json;
using System.Text.Json.Serialization;

namespace KiwiTraffic.Infrastructure.Storage;

/// <summary>
/// The single JSON shape used by every local store, so settings, cache and
/// notification files stay consistent and hand-editable.
/// </summary>
internal static class JsonStoreFormat
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,

        // Computed get-only properties (ProfileId, DisplayName, IsEmpty, ...)
        // are derived state, not configuration: never write them out.
        IgnoreReadOnlyProperties = true,

        // Keep the file stable across runs instead of emitting whatever the
        // current culture happens to use.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,

        // A misspelled key in a hand-edited file must fail loudly rather than
        // silently drop the setting the user meant to change.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}
