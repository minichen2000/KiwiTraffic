using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Tests;

public class WindowPlacementPolicyTests
{
    // A single 1920x1080 primary monitor with a 40px taskbar, in DIPs.
    private static readonly DesktopBounds Desktop = new(0, 0, 1920, 1040);

    private static WindowPlacement Placement(double left, double top, double width = 320, double height = 300)
        => new() { Left = left, Top = top, Width = width, Height = height };

    [Fact]
    public void APositionAlreadyOnScreen_IsLeftAlone()
    {
        var placement = Placement(1600, 700);

        Assert.Equal(placement, WindowPlacementPolicy.Clamp(placement, Desktop));
    }

    [Fact]
    public void APositionOnAnUnpluggedMonitor_IsPulledBackIntoView()
    {
        // Saved on a second monitor to the right that is no longer attached.
        var placement = Placement(2400, 400);

        var clamped = WindowPlacementPolicy.Clamp(placement, Desktop);

        Assert.Equal(Desktop.Right - WindowPlacementPolicy.MinimumVisible, clamped.Left);
        Assert.Equal(400, clamped.Top);
        Assert.Equal(placement.Width, clamped.Width);
    }

    [Fact]
    public void AWindowSittingAboveTheDesktop_IsPushedDown()
    {
        // A borderless widget has no title bar to grab, so hanging off the top
        // would make it impossible to recover.
        var clamped = WindowPlacementPolicy.Clamp(Placement(100, -500), Desktop);

        Assert.Equal(Desktop.Top, clamped.Top);
    }

    [Fact]
    public void AWindowHangingOffTheLeftEdge_KeepsAUsableStripVisible()
    {
        var clamped = WindowPlacementPolicy.Clamp(Placement(-2000, 100), Desktop);

        Assert.Equal(Desktop.Left - 320 + WindowPlacementPolicy.MinimumVisible, clamped.Left);
    }

    [Fact]
    public void AWindowHangingOffTheBottom_CanStillBeReachedFromAbove()
    {
        // Vertical overflow is allowed: the top of the widget stays on screen.
        var clamped = WindowPlacementPolicy.Clamp(Placement(400, 5000), Desktop);

        Assert.Equal(Desktop.Bottom - WindowPlacementPolicy.MinimumVisible, clamped.Top);
    }

    [Fact]
    public void ANarrowerDesktop_NeverLetsTheWindowEscape()
    {
        var small = new DesktopBounds(0, 0, 200, 200);

        var clamped = WindowPlacementPolicy.Clamp(Placement(5000, 5000, width: 320, height: 300), small);

        Assert.True(clamped.Left >= small.Left - 320);
        Assert.True(clamped.Left <= small.Right);
        Assert.True(clamped.Top >= small.Top);
        Assert.True(clamped.Top <= small.Bottom);
    }

    [Fact]
    public void AnUnusablePlacement_IsPassedThroughUnchanged()
    {
        var empty = new WindowPlacement { Left = 10, Top = 10, Width = 0, Height = 0 };

        Assert.Equal(empty, WindowPlacementPolicy.Clamp(empty, Desktop));
    }

    [Fact]
    public void ADesktopWithNoArea_IsPassedThroughUnchanged()
    {
        var placement = Placement(100, 100);

        Assert.Equal(placement, WindowPlacementPolicy.Clamp(placement, new DesktopBounds(0, 0, 0, 0)));
    }

    [Fact]
    public void BottomRight_PlacesTheWindowInsideTheWorkArea()
    {
        var placement = WindowPlacementPolicy.BottomRight(Desktop, width: 320, height: 300);

        Assert.Equal(Desktop.Right - 320 - 16, placement.Left);
        Assert.Equal(Desktop.Bottom - 300 - 16, placement.Top);
        Assert.True(placement.Left + placement.Width <= Desktop.Right);
        Assert.True(placement.Top + placement.Height <= Desktop.Bottom);
    }

    [Fact]
    public void BottomRight_KeepsClearingTheTaskbar()
    {
        // The work area already excludes the taskbar; the default placement
        // must not add it back.
        var placement = WindowPlacementPolicy.BottomRight(Desktop, width: 320, height: 300, margin: 0);

        Assert.Equal(1040, placement.Top + placement.Height);
    }
}
