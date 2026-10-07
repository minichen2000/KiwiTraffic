using System.Globalization;
using KiwiTraffic.App.Services;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Core.Formatting;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.KiwiVm;

namespace KiwiTraffic.App.ViewModels;

/// <summary>
/// The settings form. Holds user input as text and only converts it into an
/// <see cref="AppSettings"/> when the user saves.
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    public static IReadOnlyList<string> ProxyModeNames { get; } =
        ["使用系统默认代理", "直连（不使用代理）", "手动指定代理"];

    public static IReadOnlyList<string> IndicatorStyleNames { get; } =
        ["圆环", "进度条"];

    private readonly string? _storedProxyPassword;

    /// <summary>
    /// The configuration as it was loaded. Saving builds on top of this with
    /// <c>with</c> expressions, so fields this window does not edit - the
    /// window position, for instance - are preserved rather than reset.
    /// </summary>
    private readonly AppSettings? _original;

    private string _alias = string.Empty;
    private string _veidText = string.Empty;
    private string _apiKey = string.Empty;
    private bool _isApiKeyVisible;
    private bool _rememberApiKey = true;
    private int _proxyModeIndex;
    private string _proxyHost = string.Empty;
    private string _proxyPortText = string.Empty;
    private string _proxyUsername = string.Empty;
    private string _proxyPassword = string.Empty;
    private int _indicatorStyleIndex = (int)IndicatorStyle.Ring;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public SettingsViewModel(AppSettings? existing, StoredCredentials? credentials)
    {
        _original = existing;

        if (existing is not null)
        {
            _indicatorStyleIndex = IndexForStyle(existing.IndicatorStyle);
            _alias = existing.Alias;
            _veidText = existing.Veid > 0 ? existing.Veid.ToString(CultureInfo.InvariantCulture) : string.Empty;
            _rememberApiKey = existing.RememberApiKey;
            _proxyModeIndex = (int)existing.Proxy.Mode;
            _proxyHost = existing.Proxy.Host ?? string.Empty;
            _proxyPortText = existing.Proxy.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            _proxyUsername = existing.Proxy.Username ?? string.Empty;
        }

        _apiKey = credentials?.ApiKey ?? string.Empty;
        _storedProxyPassword = credentials?.ProxyPassword;

        IsFirstRun = existing is null || !existing.IsConfigured;
    }

    /// <summary>True when there was no usable configuration yet.</summary>
    public bool IsFirstRun { get; }

    public string WindowTitle => IsFirstRun ? "KiwiTraffic 首次配置" : "KiwiTraffic 设置";

    public string Alias
    {
        get => _alias;
        set => SetProperty(ref _alias, value);
    }

    public string VeidText
    {
        get => _veidText;
        set => SetProperty(ref _veidText, value);
    }

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    /// <summary>Reveal is a deliberate, temporary user action - never the default.</summary>
    public bool IsApiKeyVisible
    {
        get => _isApiKeyVisible;
        set => SetProperty(ref _isApiKeyVisible, value);
    }

    public bool RememberApiKey
    {
        get => _rememberApiKey;
        set => SetProperty(ref _rememberApiKey, value);
    }

    public int ProxyModeIndex
    {
        get => _proxyModeIndex;
        set
        {
            if (SetProperty(ref _proxyModeIndex, value))
            {
                OnPropertyChanged(nameof(IsManualProxy));
            }
        }
    }

    public bool IsManualProxy => ProxyModeIndex == (int)ProxyMode.Manual;

    /// <summary>Index into <see cref="IndicatorStyleNames"/>.</summary>
    public int IndicatorStyleIndex
    {
        get => _indicatorStyleIndex;
        set => SetProperty(ref _indicatorStyleIndex, value);
    }

    public string ProxyHost
    {
        get => _proxyHost;
        set => SetProperty(ref _proxyHost, value);
    }

    public string ProxyPortText
    {
        get => _proxyPortText;
        set => SetProperty(ref _proxyPortText, value);
    }

    public string ProxyUsername
    {
        get => _proxyUsername;
        set => SetProperty(ref _proxyUsername, value);
    }

    public string ProxyPassword
    {
        get => _proxyPassword;
        set => SetProperty(ref _proxyPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    /// <summary>Convenience for binding <c>IsEnabled</c>, which has no negation.</summary>
    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Converts the typed text into settings. Assumes <see cref="Validate"/>
    /// already passed.
    /// </summary>
    public AppSettings BuildSettings()
    {
        if (!SettingsValidator.TryParseVeid(VeidText, out var veid))
        {
            // Only reachable when validation was skipped or the field was
            // emptied after it passed. Zero means "not configured" - never a
            // guess at what the user meant.
            veid = 0;
        }

        var proxy = new ProxySettings
        {
            Mode = (ProxyMode)ProxyModeIndex,
            Username = string.IsNullOrWhiteSpace(ProxyUsername) ? null : ProxyUsername.Trim(),
            HasStoredPassword = HasProxyPassword(),
        };

        if (proxy.Mode == ProxyMode.Manual)
        {
            proxy = proxy with
            {
                Host = ProxyHost.Trim(),
                Port = int.TryParse(ProxyPortText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
                    ? port
                    : null,
            };
        }

        // Build on the loaded configuration rather than a fresh object: this
        // window does not edit the window position, the refresh interval, the
        // alert settings or "start with Windows", and saving must not wipe them.
        var baseline = _original ?? new AppSettings();

        return baseline with
        {
            Alias = Alias.Trim(),
            Veid = veid,
            RememberApiKey = RememberApiKey,
            Proxy = proxy,
            IndicatorStyle = IndicatorStyleIndex == (int)IndicatorStyle.Bar
                ? IndicatorStyle.Bar
                : IndicatorStyle.Ring,
        };
    }

    /// <summary>
    /// Keeps the drop-down and the enum in step. Both directions are needed,
    /// and the enum order has to match <see cref="IndicatorStyleNames"/>.
    /// </summary>
    private static int IndexForStyle(IndicatorStyle style)
        => style == IndicatorStyle.Bar ? (int)IndicatorStyle.Bar : (int)IndicatorStyle.Ring;

    /// <summary>The secrets to persist, or <c>null</c> when none should be kept.</summary>
    public StoredCredentials? BuildCredentials()
    {
        var settings = BuildSettings();

        var apiKey = RememberApiKey && !string.IsNullOrWhiteSpace(ApiKey)
            ? ApiKey.Trim()
            : null;

        var proxyPassword = HasProxyPassword() ? ProxyPassword : _storedProxyPassword;

        var credentials = new StoredCredentials
        {
            // The key is stamped with the VPS it was entered for, so it can
            // never be replayed against a different one after a switch.
            ApiKeyProfileId = apiKey is null ? null : settings.ProfileId,
            ApiKey = apiKey,
            ProxyPassword = proxyPassword,
        };

        return credentials.IsEmpty ? null : credentials;
    }

    /// <summary>Returns user-facing problems, empty when the form is acceptable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!SettingsValidator.TryParseVeid(VeidText, out _))
        {
            errors.Add("VEID 必须是正整数。");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("API Key 不能为空。");
        }

        if (Alias.Trim().Length > 64)
        {
            errors.Add("别名不能超过 64 个字符。");
        }

        if (IsManualProxy)
        {
            if (string.IsNullOrWhiteSpace(ProxyHost))
            {
                errors.Add("手动代理需要填写主机。");
            }

            if (!int.TryParse(ProxyPortText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
                port is < 1 or > 65535)
            {
                errors.Add("手动代理端口必须在 1 到 65535 之间。");
            }
        }

        return errors;
    }

    /// <summary>
    /// Queries the API with the values currently in the form. Nothing is saved,
    /// so a failed test leaves any working configuration untouched.
    /// </summary>
    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        StatusMessage = string.Empty;

        var errors = Validate();
        if (errors.Count > 0)
        {
            StatusMessage = string.Join(" ", errors);
            return;
        }

        IsBusy = true;
        try
        {
            var probe = BuildSettings();
            var result = await AppServices
                .TestConnectionAsync(probe, ApiKey.Trim(), EffectiveProxyPassword(), cancellationToken)
                .ConfigureAwait(true);

            StatusMessage = result.IsSuccess
                ? DescribeSuccess(result)
                : $"测试失败：{result.ErrorMessage}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "测试已取消。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribeSuccess(KiwiVmQueryResult result)
    {
        var usage = result.Snapshot!.ToUsage();
        var used = ByteSizeFormatter.Format(usage.UsedBytes, DisplayFormat.BytePrefix);

        if (usage.UsedPercent is not { } percent)
        {
            return $"连接成功。已用 {used}，额度未知。";
        }

        var quota = ByteSizeFormatter.Format(usage.QuotaBytes!.Value, DisplayFormat.BytePrefix);

        return $"连接成功。已用 {used} / {quota}（{percent.ToString("F1", CultureInfo.InvariantCulture)}%）。";
    }

    private bool HasProxyPassword() => !string.IsNullOrEmpty(ProxyPassword);

    private string? EffectiveProxyPassword() => HasProxyPassword() ? ProxyPassword : _storedProxyPassword;
}
