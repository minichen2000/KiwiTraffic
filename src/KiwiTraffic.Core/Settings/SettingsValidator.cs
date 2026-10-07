namespace KiwiTraffic.Core.Settings;

/// <summary>
/// Validates user-entered settings. Messages are user-facing Chinese text.
/// </summary>
public static class SettingsValidator
{
    /// <summary>
    /// Checks the fields the user types into the settings window.
    /// </summary>
    /// <param name="veid">Raw VEID input.</param>
    /// <param name="apiKey">Raw API key input. Empty is rejected.</param>
    /// <param name="alias">Optional display name.</param>
    /// <returns>An empty list when everything is acceptable.</returns>
    public static IReadOnlyList<string> ValidateConnectionFields(string? veid, string? apiKey, string? alias)
    {
        var errors = new List<string>();

        if (!TryParseVeid(veid, out _))
        {
            errors.Add("VEID 必须是正整数。");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            errors.Add("API Key 不能为空。");
        }

        if (alias is { Length: > 64 })
        {
            errors.Add("别名不能超过 64 个字符。");
        }

        return errors;
    }

    /// <summary>
    /// Parses a VEID. Only digits, no sign, no whitespace inside, and it must
    /// fit in a positive <see cref="long"/>.
    /// </summary>
    public static bool TryParseVeid(string? raw, out long veid)
    {
        veid = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        // long.TryParse would happily accept "+12", "-12" and thousands
        // separators; a VEID is plain digits only.
        foreach (var c in trimmed)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return long.TryParse(trimmed, out veid) && veid > 0;
    }

    /// <summary>Validates the whole persisted settings object.</summary>
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        if (settings.Veid <= 0)
        {
            errors.Add("VEID 必须是正整数。");
        }

        if (!AppSettings.AvailableRefreshIntervals.Contains(settings.RefreshIntervalMinutes))
        {
            errors.Add($"刷新间隔必须是 {string.Join('、', AppSettings.AvailableRefreshIntervals)} 分钟之一。");
        }

        if (settings.Proxy.Mode == ProxyMode.Manual)
        {
            if (string.IsNullOrWhiteSpace(settings.Proxy.Host))
            {
                errors.Add("手动代理需要填写主机。");
            }

            if (settings.Proxy.Port is not (>= 1 and <= 65535))
            {
                errors.Add("手动代理端口必须在 1 到 65535 之间。");
            }
        }

        foreach (var threshold in settings.Alerts.Thresholds)
        {
            if (!AlertSettings.AvailableThresholds.Contains(threshold))
            {
                errors.Add($"提醒阈值 {threshold}% 不是可选项。");
            }
        }

        return errors;
    }
}
