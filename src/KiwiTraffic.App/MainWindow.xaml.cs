using System.Windows;
using KiwiTraffic.App.ViewModels;
using KiwiTraffic.Core.Model;

namespace KiwiTraffic.App;

/// <summary>
/// M1 preview window. Replaced by the real floating widget in M3.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = CreateSimulatedViewModel();
    }

    /// <summary>
    /// A hard-coded reading so the layout can be developed before the API
    /// contract is verified. Nothing here talks to the network; the real
    /// source arrives in M2.
    /// </summary>
    private static WidgetPreviewViewModel CreateSimulatedViewModel()
    {
        var now = DateTimeOffset.Now;

        var snapshot = new TrafficSnapshot
        {
            ProfileId = "simulated",
            Veid = 0,
            UsedBytes = 612_345_678_901m,
            QuotaBytes = 1_000_000_000_000m,
            NextResetAtUtc = now.AddDays(25).ToUniversalTime(),
            FetchedAtUtc = now.ToUniversalTime(),
        };

        return new WidgetPreviewViewModel("示例 VPS（模拟）", snapshot, now);
    }
}
