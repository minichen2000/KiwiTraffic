using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using KiwiTraffic.App.Services;
using KiwiTraffic.App.ViewModels;
using KiwiTraffic.App.Views;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.Credentials;
using KiwiTraffic.Infrastructure.Storage;

namespace KiwiTraffic.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Application cannot implement IDisposable; the field is disposed in OnExit.")]
public partial class App : Application
{
    private AppServices? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // The tray icon arrives in M3; until then, closing the window is how
        // the application ends, and startup may show two windows in sequence.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _services = new AppServices();

        if (!TryLoadConfiguration(out var settings, out var credentials))
        {
            Shutdown(1);
            return;
        }

        if (settings is null || !settings.IsConfigured)
        {
            if (!RunFirstTimeSetup() || !TryLoadConfiguration(out settings, out credentials))
            {
                Shutdown();
                return;
            }
        }

        if (settings is null)
        {
            // The user cancelled out of setup.
            Shutdown();
            return;
        }

        // A key kept for a different VPS counts as no key at all, rather than
        // being quietly sent to the wrong account.
        var apiKey = credentials?.ApiKeyBelongsTo(settings) == true ? credentials.ApiKey : null;

        var session = _services.ConfigureSession(settings, apiKey, credentials?.ProxyPassword);

        var window = new MainWindow(_services, session, settings, apiKey);
        MainWindow = window;
        window.Closed += (_, _) => Shutdown();
        window.Show();

        _ = window.LoadAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Reads the stored configuration. Returns <c>false</c> only for problems
    /// the user cannot recover from by re-entering settings.
    /// </summary>
    private bool TryLoadConfiguration(out AppSettings? settings, out StoredCredentials? credentials)
    {
        settings = null;
        credentials = null;

        try
        {
            // Startup path: two small local file reads before any UI exists.
            // Blocking here avoids an async startup dance and cannot deadlock.
            settings = _services!.SettingsStore.LoadAsync().GetAwaiter().GetResult();

            var result = _services.CredentialStore.LoadAsync().GetAwaiter().GetResult();
            credentials = result.Credentials;

            if (result.Status == CredentialLoadStatus.Undecryptable)
            {
                MessageBox.Show(
                    "本机保存的 API Key 无法解密。这通常发生在把程序拷到另一台电脑，或改用另一个 Windows 用户之后。"
                    + "请重新输入一次。",
                    "需要重新输入 API Key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return true;
        }
        catch (CorruptStoreException ex)
        {
            // The original has already been moved aside; carry on as a first run
            // rather than silently starting from defaults.
            MessageBox.Show(
                ex.Message,
                "配置文件无法使用",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法读取本机配置：{ex.Message}",
                "KiwiTraffic",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    private bool RunFirstTimeSetup()
    {
        var editor = new SettingsWindow(_services!, new SettingsViewModel(existing: null, credentials: null));

        return editor.ShowDialog() == true;
    }

    /// <summary>
    /// A WinExe has no console, so an unhandled exception would otherwise be
    /// completely silent (it only surfaces in the Windows event log). Report it
    /// and exit with a non-zero code instead of pretending to keep running.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.ToString(),
            "KiwiTraffic 发生未处理的异常",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Shutdown(1);
    }
}
