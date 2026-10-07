using System.Windows;
using KiwiTraffic.App.Services;
using KiwiTraffic.App.ViewModels;
using KiwiTraffic.App.Views;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure;
using KiwiTraffic.Infrastructure.Credentials;

namespace KiwiTraffic.App;

/// <summary>
/// The widget content for M2. The floating behaviour - drag, always-on-top,
/// tray, remembering its position - arrives with M3.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppServices _services;

    private WidgetViewModel _viewModel;
    private UsageSession _session;
    private AppSettings _settings;
    private string? _apiKey;

    public MainWindow(AppServices services, UsageSession session, AppSettings settings, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        _session = session;
        _settings = settings;
        _apiKey = apiKey;
        _viewModel = WidgetViewModel.Loading(settings.DisplayName);

        InitializeComponent();
        DataContext = _viewModel;
    }

    /// <summary>Shows whatever was cached, then asks the API.</summary>
    public async Task LoadAsync()
    {
        try
        {
            await ShowCachedAsync();
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"读取上次数据失败：{ex.Message}";
        }

        await RefreshAsync();
    }

    private async Task ShowCachedAsync()
    {
        var cached = await _session.LoadCachedAsync();
        if (cached is null)
        {
            return;
        }

        _viewModel.Apply(cached.Snapshot.ToUsage(), cached.Snapshot, DateTimeOffset.Now);
        _viewModel.StatusMessage = "显示的是上次成功获取的数据，正在更新…";
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _viewModel.StatusMessage = "尚未保存可用的 API Key，请在设置中填写。";
            return;
        }

        _viewModel.IsBusy = true;
        try
        {
            var result = await _session.RefreshAsync();

            if (result.IsSuccess)
            {
                var snapshot = result.Snapshot!;
                _viewModel.Apply(snapshot.ToUsage(), snapshot, DateTimeOffset.Now);
                _viewModel.StatusMessage = string.Empty;
            }
            else
            {
                // Whatever was on screen stays: a failed refresh must never
                // look like a reading of zero.
                _viewModel.StatusMessage = $"更新失败：{result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"更新时发生未预期的错误：{ex.Message}";
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var credentials = await LoadCredentialsAsync();

        var editor = new SettingsWindow(_services, new SettingsViewModel(_settings, credentials))
        {
            Owner = this,
        };

        // Cancelling must leave the running configuration exactly as it was.
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await ReloadConfigurationAsync();
        await RefreshAsync();
    }

    private async Task ReloadConfigurationAsync()
    {
        var settings = await _services.SettingsStore.LoadAsync();
        if (settings is null)
        {
            return;
        }

        var credentials = await LoadCredentialsAsync();

        _settings = settings;
        _apiKey = credentials?.ApiKeyBelongsTo(settings) == true ? credentials.ApiKey : null;

        // Rebuilding the session cancels anything still in flight for the old
        // configuration, so its response can no longer be published.
        _session = _services.ConfigureSession(settings, _apiKey, credentials?.ProxyPassword);

        _viewModel = WidgetViewModel.Loading(settings.DisplayName);
        DataContext = _viewModel;
    }

    private async Task<StoredCredentials?> LoadCredentialsAsync()
    {
        var result = await _services.CredentialStore.LoadAsync();

        return result.Status switch
        {
            CredentialLoadStatus.Loaded => result.Credentials,
            CredentialLoadStatus.Undecryptable => null,
            _ => null,
        };
    }
}
