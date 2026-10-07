using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiwiTraffic.App.Services;
using KiwiTraffic.App.ViewModels;
using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.App.Views;

/// <summary>
/// First-run setup and later edits. Both go through the same window: the only
/// difference is the title and whether cancelling leaves a usable state.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppServices _services;
    private readonly SettingsViewModel _viewModel;

    /// <summary>The theme in effect when the window opened, to restore on cancel.</summary>
    private readonly AppTheme _originalTheme;

    public SettingsWindow(AppServices services, SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(viewModel);

        _services = services;
        _viewModel = viewModel;
        _originalTheme = ThemeManager.Current;

        InitializeComponent();

        DataContext = viewModel;

        // PasswordBox cannot be bound, so the two password fields are primed
        // and kept in sync by hand.
        ApiKeyBox.Password = viewModel.ApiKey;
        ProxyPasswordBox.Password = viewModel.ProxyPassword;

        // The theme chips preview their choice immediately - being able to see
        // it beats reading a label.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// A borderless dialog owned by an always-on-top widget has to be on top
    /// too, or it would open behind the thing that opened it.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (Owner is { Topmost: true })
        {
            Topmost = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Covers Cancel, the title-bar ✕ and Esc alike: anything other than a
        // confirmed save puts the previous theme back.
        if (DialogResult != true)
        {
            ThemeManager.Apply(_originalTheme);
        }

        base.OnClosing(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedTheme))
        {
            ThemeManager.Apply(_viewModel.SelectedTheme);
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
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
