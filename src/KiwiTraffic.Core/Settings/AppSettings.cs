namespace KiwiTraffic.Core.Settings;

/// <summary>How the HTTP client picks up a proxy.</summary>
public enum ProxyMode
{
    /// <summary>Use the .NET default resolution (system/IE settings).</summary>
    System,

    /// <summary>Ignore any proxy and connect directly.</summary>
    Direct,

    /// <summary>Use the explicit <see cref="ProxySettings.Host"/>/<see cref="ProxySettings.Port"/>.</summary>
    Manual,
}

/// <summary>Proxy configuration. Passwords never live here - see the credential store.</summary>
public sealed record ProxySettings
{
    public ProxyMode Mode { get; init; } = ProxyMode.System;

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Username { get; init; }

    /// <summary>
    /// Whether a proxy password is stored in the encrypted credential file.
    /// The password itself is never written to settings.json.
    /// </summary>
    public bool HasStoredPassword { get; init; }
}

/// <summary>Traffic threshold alerts.</summary>
public sealed record AlertSettings
{
    /// <summary>Thresholds the user can pick from, in percent. Order is meaningful (ascending).</summary>
    public static readonly IReadOnlyList<int> AvailableThresholds = [80, 90, 95];

    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Percentages that raise an alert. Only values from
    /// <see cref="AvailableThresholds"/> are meaningful.
    /// </summary>
    public IReadOnlyList<int> Thresholds { get; init; } = [.. AvailableThresholds];
}

/// <summary>
/// Everything persisted to <c>settings.json</c>. Contains no secrets.
/// </summary>
public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public const int DefaultRefreshIntervalMinutes = 5;

    /// <summary>Intervals the UI offers. The value actually used must also respect rate limits.</summary>
    public static readonly IReadOnlyList<int> AvailableRefreshIntervals = [5, 10, 15, 30, 60];

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Optional display name. May be empty.</summary>
    public string Alias { get; init; } = string.Empty;

    /// <summary>The KiwiVM VEID. Zero means "not configured yet".</summary>
    public long Veid { get; init; }

    /// <summary>Whether the API key is kept on this machine at all.</summary>
    public bool RememberApiKey { get; init; } = true;

    public ProxySettings Proxy { get; init; } = new();

    public int RefreshIntervalMinutes { get; init; } = DefaultRefreshIntervalMinutes;

    public bool StartWithWindows { get; init; }

    public bool AlwaysOnTop { get; init; } = true;

    /// <summary>Which indicator the widget draws. Both show the same number.</summary>
    public IndicatorStyle IndicatorStyle { get; init; } = IndicatorStyle.Ring;

    /// <summary>Light or dark colour scheme.</summary>
    public AppTheme Theme { get; init; } = AppTheme.Light;

    /// <summary>
    /// Where the widget was last seen, or <c>null</c> when it has never been
    /// moved. Restoring is always clamped to the current desktop.
    /// </summary>
    public WindowPlacement? Placement { get; init; }

    public AlertSettings Alerts { get; init; } = new();

    /// <summary>True once a VEID has been entered.</summary>
    public bool IsConfigured => Veid > 0;

    /// <summary>
    /// Identity of this configuration. Cached readings are valid only for the
    /// profile that produced them, so switching VPS can never surface the
    /// previous machine's numbers.
    /// </summary>
    public string ProfileId => $"veid:{Veid}";

    /// <summary>The name to show in the UI, falling back to something identifiable.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? $"VPS {Veid}" : Alias.Trim();
}
