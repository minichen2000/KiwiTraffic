using System.Windows;
using System.Windows.Threading;

namespace KiwiTraffic.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    /// <summary>
    /// A WinExe has no console, so an unhandled exception at startup would
    /// otherwise be completely silent (it only surfaces in the Windows event
    /// log). Report it and exit with a non-zero code instead of pretending to
    /// keep running.
    /// </summary>
    /// <remarks>
    /// Development aid. Revisit once the real error/status surface exists
    /// (M3/M4), which should report failures in the widget itself.
    /// </remarks>
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
