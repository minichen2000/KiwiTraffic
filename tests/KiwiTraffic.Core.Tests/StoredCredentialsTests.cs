using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.Core.Tests;

public class StoredCredentialsTests
{
    private static readonly AppSettings VpsOne = new() { Veid = 1 };
    private static readonly AppSettings VpsTwo = new() { Veid = 2 };

    [Fact]
    public void KeyEnteredForThisVps_MayBeUsed()
    {
        var credentials = new StoredCredentials { ApiKeyProfileId = "veid:1", ApiKey = "private_key" };

        Assert.True(credentials.ApiKeyBelongsTo(VpsOne));
    }

    [Fact]
    public void KeyEnteredForAnotherVps_IsNotReplayed()
    {
        var credentials = new StoredCredentials { ApiKeyProfileId = "veid:1", ApiKey = "private_key" };

        Assert.False(credentials.ApiKeyBelongsTo(VpsTwo));
    }

    [Fact]
    public void KeyWithoutAProfileStamp_IsNotUsed()
    {
        // Written before the stamp existed: unusable rather than risky.
        var credentials = new StoredCredentials { ApiKey = "private_key" };

        Assert.False(credentials.ApiKeyBelongsTo(VpsOne));
    }

    [Fact]
    public void NoStoredKey_DoesNotCount()
    {
        Assert.False(new StoredCredentials { ApiKeyProfileId = "veid:1" }.ApiKeyBelongsTo(VpsOne));
        Assert.False(new StoredCredentials().ApiKeyBelongsTo(VpsOne));
    }

    [Fact]
    public void ProxyPasswordAlone_IsEnoughToBeWorthStoring()
    {
        var credentials = new StoredCredentials { ProxyPassword = "proxy-secret" };

        Assert.False(credentials.IsEmpty);
        Assert.False(credentials.ApiKeyBelongsTo(VpsOne));
    }
}
