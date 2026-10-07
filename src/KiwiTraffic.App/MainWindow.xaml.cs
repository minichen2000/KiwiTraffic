using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KiwiTraffic.App.Services;
using KiwiTraffic.App.ViewModels;
using KiwiTraffic.App.Views;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure;
using KiwiTraffic.Infrastructure.Credentials;

namespace KiwiTraffic.App;

/// <summary>
/// The floating widget: draggable, borderless, remembers where it was, and
/// lives in the tray between refreshes.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly DispatcherTimer _placementSaveTimer;
    private readonly DispatcherTimer _clockTimer;

    private readonly TrayIcon _tray;
    private WidgetViewModel _viewModel;
    private UsageSession _session;
    private AppSettings _settings;
    private string? _apiKey;
    private bool _positioned;
    private bool _allowClose;
    private bool _hideHintShown;

    public MainWindow(AppServices services, UsageSession session, AppSettings settings, string? apiKey, TrayIcon tray)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tray);

        _services = services;
        _tray = tray;
        _session = session;
        _settings = settings;
        _apiKey = apiKey;
        _viewModel = WidgetViewModel.Loading(settings.DisplayName, settings.IndicatorStyle);

        InitializeComponent();

        DataContext = _viewModel;
        Topmost = settings.AlwaysOnTop;

        // Positioned after the first layout pass, once the real size is known;
        // invisible until then so it never flashes at the wrong corner.
        Opacity = 0;

        _placementSaveTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(600),
            DispatcherPriority.Background,
            OnPlacementSaveTick,
            Dispatcher);
        _placementSaveTimer.Stop();

        // The relative labels have to keep moving. Leaving them frozen would
        // let the widget claim "just now" long after the reading went stale -
        // the plan calls that out by name.
        _clockTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(20),
            DispatcherPriority.Background,
            OnClockTick,
            Dispatcher);
        _clockTimer.Start();

        LocationChanged += OnGeometryChanged;
        SizeChanged += OnGeometryChanged;

        _tray.ToggleRequested += (_, _) => ToggleVisibility();
        _tray.RefreshRequested += (_, _) => _ = RefreshAsync();
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.ResetPositionRequested += (_, _) => ResetPosition();
        _tray.TopmostChanged += OnTrayTopmostChanged;
        _tray.ExitRequested += (_, _) => ExitApplication();
        IsVisibleChanged += (_, _) => _tray.SetWindowVisible(IsVisible);
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

    // --- window placement -----------------------------------------------------

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        PositionWindow();
        Opacity = 1;
    }

    /// <summary>Puts the widget back at the bottom-right of the work area.</summary>
    public void ResetPosition()
    {
        var target = WindowPlacementPolicy.BottomRight(PrimaryWorkArea(), ActualWidth, ActualHeight);

        Left = target.Left;
        Top = target.Top;

        _placementSaveTimer.Stop();
        _ = SavePlacementAsync();
    }

    private void PositionWindow()
    {
        var target = _settings.Placement is { IsUsable: true } saved
            // Keep the corner the user chose, but at the size the window has
            // now: the height changes with the content.
            ? saved with
            {
                Left = saved.Left + saved.Width - ActualWidth,
                Top = saved.Top + saved.Height - ActualHeight,
                Width = ActualWidth,
                Height = ActualHeight,
            }
            // The plan asks for the primary work area, not the whole virtual
            // desktop: with a second monitor to the right, the virtual
            // bottom-right corner is on the wrong screen.
            : WindowPlacementPolicy.BottomRight(PrimaryWorkArea(), ActualWidth, ActualHeight);

        var clamped = WindowPlacementPolicy.Clamp(target, VirtualScreen());

        Left = clamped.Left;
        Top = clamped.Top;

        _positioned = true;
    }

    /// <summary>The primary monitor's work area - device-independent pixels.</summary>
    private static DesktopBounds PrimaryWorkArea()
    {
        var work = SystemParameters.WorkArea;

        return new DesktopBounds(work.Left, work.Top, work.Width, work.Height);
    }

    /// <summary>
    /// The whole virtual desktop, used only to keep a restored position from
    /// landing off every screen. Per-monitor work areas are not modelled here:
    /// in a mixed-DPI setup the single DIP rectangle is an approximation, which
    /// is why the tray has "restore window position".
    /// </summary>
    private static DesktopBounds VirtualScreen() => new(
        SystemParameters.VirtualScreenLeft,
        SystemParameters.VirtualScreenTop,
        SystemParameters.VirtualScreenWidth,
        SystemParameters.VirtualScreenHeight);

    private void OnGeometryChanged(object? sender, EventArgs e)
    {
        if (!_positioned)
        {
            return;
        }

        KeepWithinDesktop();

        _placementSaveTimer.Stop();
        _placementSaveTimer.Start();
    }

    /// <summary>
    /// The content grows and shrinks (a status box appears, a line wraps). A
    /// widget anchored near the bottom must not slide off the screen when it does.
    /// </summary>
    private void KeepWithinDesktop()
    {
        var desktop = VirtualScreen();

        if (ActualHeight > 0 && Top + ActualHeight > desktop.Bottom)
        {
            Top = Math.Max(desktop.Top, desktop.Bottom - ActualHeight);
        }

        if (ActualWidth > 0 && Left + ActualWidth > desktop.Right)
        {
            Left = Math.Max(desktop.Left, desktop.Right - ActualWidth);
        }
    }

    private void OnClockTick(object? sender, EventArgs e)
        => _viewModel.RefreshRelativeTimes(DateTimeOffset.Now);

    private async void OnPlacementSaveTick(object? sender, EventArgs e)
    {
        _placementSaveTimer.Stop();
        await SavePlacementAsync();
    }

    private async Task SavePlacementAsync()
    {
        try
        {
            var placement = new WindowPlacement
            {
                Left = Left,
                Top = Top,
                Width = ActualWidth,
                Height = ActualHeight,
            };

            if (_settings.Placement == placement)
            {
                return;
            }

            _settings = _settings with { Placement = placement };
            await _services.SettingsStore.SaveAsync(_settings);
        }
        catch (Exception)
        {
            // Forgetting where the window was is not worth interrupting the user.
        }
    }

    // --- window behaviour -----------------------------------------------------

    /// <summary>
    /// Shows the tooltip only when the name is actually cut off - a tooltip
    /// repeating text that is already fully visible is just noise.
    /// </summary>
    private void OnAliasToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (sender is TextBlock block && !IsTextTrimmed(block))
        {
            e.Handled = true;
        }
    }

    private static bool IsTextTrimmed(TextBlock block)
    {
        if (string.IsNullOrEmpty(block.Text) || block.ActualWidth <= 0)
        {
            return false;
        }

        var typeface = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);

        var measured = new FormattedText(
            block.Text,
            CultureInfo.CurrentUICulture,
            block.FlowDirection,
            typeface,
            block.FontSize,
            block.Foreground,
            VisualTreeHelper.GetDpi(block).PixelsPerDip);

        return measured.Width > block.ActualWidth;
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        // A borderless window has no title bar to drag; this is the title bar.
        DragMove();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            // The tray icon belongs to the application, which disposes it on exit.
            _placementSaveTimer.Stop();
            _clockTimer.Stop();

            base.OnClosing(e);
            return;
        }

        // Closing the window keeps the widget running: the tray icon is the
        // way back, and the menu has an explicit Exit.
        e.Cancel = true;
        HideToTray();
    }

    private void OnHide(object sender, RoutedEventArgs e) => HideToTray();

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            HideToTray();
        }
        else
        {
            ShowFromTray();
        }
    }

    /// <summary>Brings the widget back, e.g. after a second launch asked for it.</summary>
    public void ShowFromTray()
    {
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void HideToTray()
    {
        _placementSaveTimer.Stop();
        _ = SavePlacementAsync();
        Hide();

        if (!_hideHintShown)
        {
            _hideHintShown = true;
            _tray.ShowHint("KiwiTraffic 仍在运行", "窗口已隐藏到托盘。双击托盘图标可以重新打开，或在托盘菜单里退出。");
        }
    }

    private void ExitApplication()
    {
        _allowClose = true;
        Close();
    }

    private void OnTrayTopmostChanged(bool topmost)
    {
        Topmost = topmost;
        _settings = _settings with { AlwaysOnTop = topmost };
        _ = PersistSettingsAsync();
    }

    private async Task PersistSettingsAsync()
    {
        try
        {
            await _services.SettingsStore.SaveAsync(_settings);
        }
        catch (Exception)
        {
            // The toggle still applies for this session.
        }
    }

    // --- data -----------------------------------------------------------------

    private async Task ShowCachedAsync()
    {
        var cached = await _session.LoadCachedAsync();
        if (cached is null)
        {
            return;
        }

        _viewModel.Apply(cached.Snapshot.ToUsage(), cached.Snapshot, DateTimeOffset.Now);
        _viewModel.StatusMessage = "显示的是上次成功获取的数据，正在更新…";
        UpdateTrayStatus();
    }

    private async Task RefreshAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _viewModel.StatusMessage = "尚未保存可用的 API Key，请在设置中填写。";
            UpdateTrayStatus();
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

            UpdateTrayStatus();
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"更新时发生未预期的错误：{ex.Message}";
            UpdateTrayStatus();
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private void UpdateTrayStatus()
        => _tray.UpdateStatus(_viewModel.Alias, _viewModel.PercentText, _viewModel.Level, _viewModel.StatusMessage);

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenSettings()
    {
        var settings = _settings;
        _ = OpenSettingsAsync(settings);
    }

    private async Task OpenSettingsAsync(AppSettings settings)
    {
        var credentials = await LoadCredentialsAsync();

        var editor = new SettingsWindow(_services, new SettingsViewModel(settings, credentials))
        {
            Owner = IsVisible ? this : null,
        };

        // Cancelling must leave the running configuration exactly as it was.
        if (editor.ShowDialog() != true)
        {
            return;
        }

        await ReloadConfigurationAsync();
        await RefreshAsync();
        UpdateTrayStatus();
    }

    private async void OnOpenSettings(object sender, RoutedEventArgs e) => await OpenSettingsAsync(_settings);

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

        Topmost = settings.AlwaysOnTop;
        _tray.SetTopmost(settings.AlwaysOnTop);
        ThemeManager.Apply(settings.Theme);

        _viewModel = WidgetViewModel.Loading(settings.DisplayName, settings.IndicatorStyle);
        DataContext = _viewModel;

        // The widget may change height with the content; make sure it is still
        // fully on screen before remembering the new geometry.
        KeepWithinDesktop();
    }

    private async Task<StoredCredentials?> LoadCredentialsAsync()
    {
        var result = await _services.CredentialStore.LoadAsync();

        return result.Status == CredentialLoadStatus.Loaded ? result.Credentials : null;
    }
}
