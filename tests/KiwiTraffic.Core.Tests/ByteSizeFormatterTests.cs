using KiwiTraffic.Core.Formatting;

namespace KiwiTraffic.Core.Tests;

public class ByteSizeFormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(999, "999 B")]
    [InlineData(1000, "1.0 KB")]
    [InlineData(1500, "1.5 KB")]
    [InlineData(1_000_000, "1.0 MB")]
    [InlineData(1_000_000_000, "1.0 GB")]
    [InlineData(1_000_000_000_000, "1.0 TB")]
    public void SiStyle_UsesThousandPowers(decimal bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes, BytePrefixStyle.Si));
    }

    [Theory]
    [InlineData(999, "999 B")]
    [InlineData(1000, "1.0 KiB")]
    [InlineData(1024, "1.0 KiB")]
    [InlineData(1_048_576, "1.0 MiB")]
    [InlineData(1_073_741_824, "1.0 GiB")]
    [InlineData(1_099_511_627_776, "1.0 TiB")]
    public void IecStyle_DividesBy1024(decimal bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes, BytePrefixStyle.Iec));
    }

    [Fact]
    public void IecStyle_StepsUpAt1000_NotAt1024()
    {
        // Dividing by 1024 but stepping up at 1024 would print "1023.9 MiB" for
        // the value just below a unit, which nobody wants to read.
        Assert.Equal("999.0 MiB", ByteSizeFormatter.Format(999m * 1024 * 1024, BytePrefixStyle.Iec));
        Assert.Equal("1.0 GiB", ByteSizeFormatter.Format(1000m * 1024 * 1024, BytePrefixStyle.Iec));
    }

    [Fact]
    public void IecStyle_MatchesHowTheKiwiVmPanelDescribesAQuota()
    {
        // Live account: plan_monthly_data is 1000 x 1024^3 bytes and the panel
        // calls that "1 TB". Dividing by 1024 and stepping up at 1000 gives the
        // same reading in an honest unit.
        const decimal kiwiVmOneTerabyteQuota = 1000m * 1024 * 1024 * 1024;

        Assert.Equal("1.0 TiB", ByteSizeFormatter.Format(kiwiVmOneTerabyteQuota, BytePrefixStyle.Iec));

        // ... even though it is not a whole tebibyte.
        Assert.Equal(0.977m, Math.Round(kiwiVmOneTerabyteQuota / (1024m * 1024 * 1024 * 1024), 3));

        // The same quantity in decimal units is where the confusing "1.1 TB"
        // came from - that reading is arithmetically right but matches nothing
        // the provider shows, which is why SI is not used for this.
        Assert.Equal("1.1 TB", ByteSizeFormatter.Format(kiwiVmOneTerabyteQuota, BytePrefixStyle.Si));
    }

    [Fact]
    public void TheTwoStylesDisagreeOnAKiwiVmQuota_WhichIsWhyTheChoiceIsPinned()
    {
        // 2^40 bytes, the quota a KiwiVM "1 TB" plan reports. The panel shows
        // "1 TB" for it, so the binary reading is the one that matches; the
        // decimal reading would say 1.1 TB. See DisplayFormat.BytePrefix.
        const decimal oneTebibyte = 1_099_511_627_776m;

        Assert.Equal("1.0 TiB", ByteSizeFormatter.Format(oneTebibyte, BytePrefixStyle.Iec));
        Assert.Equal("1.1 TB", ByteSizeFormatter.Format(oneTebibyte, BytePrefixStyle.Si));
    }

    [Fact]
    public void IecStyle_NeverLabelsBinaryUnitsAsDecimal()
    {
        // 1 GiB is the classic case where a decimal label would be wrong.
        var text = ByteSizeFormatter.Format(1_073_741_824m, BytePrefixStyle.Iec);

        Assert.Equal("1.0 GiB", text);
        Assert.DoesNotContain("GB", text);

        // The same byte count really is ~1.07 GB in SI units.
        Assert.Equal("1.1 GB", ByteSizeFormatter.Format(1_073_741_824m, BytePrefixStyle.Si));
    }

    [Fact]
    public void FixedFractionDigits_AreHonoured()
    {
        Assert.Equal("1.23 GB", ByteSizeFormatter.Format(1_234_567_890m, BytePrefixStyle.Si, decimals: 2));
        Assert.Equal("1 GB", ByteSizeFormatter.Format(1_234_567_890m, BytePrefixStyle.Si, decimals: 0));
    }

    [Fact]
    public void ByteValues_IgnoreTheFractionDigitSetting()
    {
        Assert.Equal("512 B", ByteSizeFormatter.Format(512m, BytePrefixStyle.Si, decimals: 3));
    }

    [Fact]
    public void NegativeByteCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSizeFormatter.Format(-1m, BytePrefixStyle.Si));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void OutOfRangeFractionDigits_Throw(int decimals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ByteSizeFormatter.Format(1000m, BytePrefixStyle.Si, decimals));
    }
}
