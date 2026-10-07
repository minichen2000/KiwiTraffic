using System.Windows;
using System.Windows.Controls;
using KiwiTraffic.App.Services;
using KiwiTraffic.App.ViewModels;

namespace KiwiTraffic.App.Views;

/// <summary>
/// First-run setup and later edits. Both go through the same window: the only
/// difference is the title and whether cancelling leaves a usable state.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppServices _services;
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(AppServices services, SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(viewModel);

        _services = services;
        _viewModel = viewModel;

        InitializeComponent();

        DataContext = viewModel;

        // PasswordBox cannot be bound, so the two password fields are primed
        // and kept in sync by hand.
        ApiKeyBox.Password = viewModel.ApiKey;
        ProxyPasswordBox.Password = viewModel.ProxyPassword;
    }

    private void OnApiKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.ApiKey = ApiKeyBox.Password;
        }
    }

    private void OnProxyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.ProxyPassword = ProxyPasswordBox.Password;
        }
    }

    /// <summary>
    /// Leaving the revealed state: whatever was typed into the plain text box
    /// has to be carried back into the masked one.
    /// </summary>
    private void OnRevealApiKeyToggled(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: false } && DataContext is SettingsViewModel viewModel)
        {
            ApiKeyBox.Password = viewModel.ApiKey;
        }
    }

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        try
        {
            // Uses what is on screen and saves nothing, so a failed test can
            // never disturb a configuration that already works.
            await _viewModel.TestConnectionAsync();
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"测试时发生未预期的错误：{ex.Message}";
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var errors = _viewModel.Validate();
        if (errors.Count > 0)
        {
            _viewModel.StatusMessage = string.Join(" ", errors);
            return;
        }

        var settings = _viewModel.BuildSettings();
        var credentials = _viewModel.BuildCredentials();

        try
        {
            // Secrets first. If the settings write then fails, the stored
            // configuration still describes the VPS the stored key is stamped
            // for, so the app asks for the key again instead of pairing a new
            // VEID with an old key.
            if (credentials is null)
            {
                await _services.CredentialStore.DeleteAsync();
            }
            else
            {
                await _services.CredentialStore.SaveAsync(credentials);
            }

            await _services.SettingsStore.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"保存失败：{ex.Message}";
            return;
        }

        DialogResult = true;
    }
}
