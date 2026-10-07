using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Tests;

public class AppThemeTests
{
    [Fact]
    public void NumericValues_ArePinnedBecauseTheyArePersisted()
    {
        // Same rule as IndicatorStyle: these numbers go into settings.json and
        // are reused as drop-down indices.
        Assert.Equal(0, (int)AppTheme.Light);
        Assert.Equal(1, (int)AppTheme.Dark);
    }

    [Fact]
    public void LightIsTheDefault()
    {
        Assert.Equal(AppTheme.Light, new AppSettings().Theme);
    }
}
