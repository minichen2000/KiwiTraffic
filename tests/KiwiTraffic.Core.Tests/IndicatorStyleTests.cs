using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Tests;

public class IndicatorStyleTests
{
    [Fact]
    public void NumericValues_ArePinnedBecauseTheyArePersisted()
    {
        // These go into settings.json as numbers and are also used as the
        // indices of the settings drop-down. Reordering the enum would silently
        // turn a saved ring into a bar, so the values are part of the format.
        Assert.Equal(0, (int)IndicatorStyle.Ring);
        Assert.Equal(1, (int)IndicatorStyle.Bar);
    }

    [Fact]
    public void RingIsTheDefault()
    {
        Assert.Equal(IndicatorStyle.Ring, new AppSettings().IndicatorStyle);
        Assert.Equal((int)IndicatorStyle.Ring, (int)new AppSettings().IndicatorStyle);
    }
}
