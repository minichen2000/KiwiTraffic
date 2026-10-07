using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Tests;

public class SettingsValidatorTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("12345")]
    [InlineData("9223372036854775807")] // long.MaxValue
    public void ValidVeid_IsAccepted(string raw)
    {
        Assert.True(SettingsValidator.TryParseVeid(raw, out var veid));
        Assert.True(veid > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]          // long.TryParse would accept this
    [InlineData("1 2")]
    [InlineData(" 12 34 ")]     // inner whitespace
    [InlineData("12.5")]
    [InlineData("abc")]
    [InlineData("0x10")]
    [InlineData("١٢٣")]          // non-ASCII digits must not slip through
    [InlineData("9223372036854775808")] // long.MaxValue + 1
    public void InvalidVeid_IsRejected(string? raw)
    {
        Assert.False(SettingsValidator.TryParseVeid(raw, out var veid));
        Assert.Equal(0, veid);
    }

    [Fact]
    public void ValidConnectionFields_ProduceNoErrors()
    {
        var errors = SettingsValidator.ValidateConnectionFields("12345", "private_abc", "别名");

        Assert.Empty(errors);
    }

    [Fact]
    public void BlankApiKey_IsRejected()
    {
        var errors = SettingsValidator.ValidateConnectionFields("12345", "   ", null);

        Assert.Contains(errors, message => message.Contains("API Key", StringComparison.Ordinal));
    }

    [Fact]
    public void BadVeidAndBlankKey_ReportBothProblems()
    {
        var errors = SettingsValidator.ValidateConnectionFields("abc", "", null);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void OverlongAlias_IsRejected()
    {
        var errors = SettingsValidator.ValidateConnectionFields("12345", "private_abc", new string('a', 65));

        Assert.Contains(errors, message => message.Contains("别名", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultSettingsWithAVeid_AreValid()
    {
        var errors = SettingsValidator.Validate(new AppSettings { Veid = 12345 });

        Assert.Empty(errors);
    }

    [Fact]
    public void UnconfiguredSettings_AreInvalid()
    {
        var errors = SettingsValidator.Validate(new AppSettings());

        Assert.Contains(errors, message => message.Contains("VEID", StringComparison.Ordinal));
    }

    [Fact]
    public void RefreshIntervalOutsideTheOfferedSet_IsRejected()
    {
        var errors = SettingsValidator.Validate(new AppSettings { Veid = 1, RefreshIntervalMinutes = 7 });

        Assert.Contains(errors, message => message.Contains("刷新间隔", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, 8080)]
    [InlineData("proxy.local", null)]
    [InlineData("proxy.local", 0)]
    [InlineData("proxy.local", 65536)]
    public void IncompleteManualProxy_IsRejected(string? host, int? port)
    {
        var settings = new AppSettings
        {
            Veid = 1,
            Proxy = new ProxySettings { Mode = ProxyMode.Manual, Host = host, Port = port },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, message => message.Contains("代理", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteManualProxy_IsAccepted()
    {
        var settings = new AppSettings
        {
            Veid = 1,
            Proxy = new ProxySettings { Mode = ProxyMode.Manual, Host = "127.0.0.1", Port = 10808 },
        };

        Assert.Empty(SettingsValidator.Validate(settings));
    }

    [Fact]
    public void UnknownAlertThreshold_IsRejected()
    {
        var settings = new AppSettings
        {
            Veid = 1,
            Alerts = new AlertSettings { Enabled = true, Thresholds = [80, 85] },
        };

        var errors = SettingsValidator.Validate(settings);

        Assert.Contains(errors, message => message.Contains("85", StringComparison.Ordinal));
    }

    [Fact]
    public void ProfileId_IsDerivedFromTheVeid()
    {
        // Cached readings are keyed on this, so it must change when the VPS does.
        Assert.Equal("veid:1", new AppSettings { Veid = 1 }.ProfileId);
        Assert.NotEqual(new AppSettings { Veid = 1 }.ProfileId, new AppSettings { Veid = 2 }.ProfileId);
    }

    [Theory]
    [InlineData("", "VPS 7")]
    [InlineData("   ", "VPS 7")]
    [InlineData("家里的", "家里的")]
    [InlineData("  家里的  ", "家里的")]
    public void DisplayName_FallsBackToTheVeid(string alias, string expected)
    {
        Assert.Equal(expected, new AppSettings { Veid = 7, Alias = alias }.DisplayName);
    }
}
