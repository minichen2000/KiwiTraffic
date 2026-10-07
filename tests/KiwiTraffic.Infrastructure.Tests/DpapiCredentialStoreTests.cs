using System.Text;
using KiwiTraffic.Core.Credentials;
using KiwiTraffic.Infrastructure.Credentials;
using KiwiTraffic.Infrastructure.Tests.TestSupport;

namespace KiwiTraffic.Infrastructure.Tests;

public class DpapiCredentialStoreTests
{
    private const string ApiKey = "private_example_key_do_not_log";
    private const string ProxyPassword = "proxy-secret";

    [Fact]
    public async Task NoFileYet_ReportsMissing()
    {
        using var temp = new TempDirectory();

        var result = await new DpapiCredentialStore(temp.ToAppPaths()).LoadAsync();

        Assert.Equal(CredentialLoadStatus.Missing, result.Status);
        Assert.Null(result.Credentials);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsBothSecrets()
    {
        using var temp = new TempDirectory();
        var store = new DpapiCredentialStore(temp.ToAppPaths());

        await store.SaveAsync(new StoredCredentials { ApiKey = ApiKey, ProxyPassword = ProxyPassword });
        var result = await store.LoadAsync();

        Assert.Equal(CredentialLoadStatus.Loaded, result.Status);
        Assert.Equal(ApiKey, result.Credentials!.ApiKey);
        Assert.Equal(ProxyPassword, result.Credentials.ProxyPassword);
    }

    [Fact]
    public async Task StoredFile_IsNotReadableAsPlaintext()
    {
        using var temp = new TempDirectory();
        var store = new DpapiCredentialStore(temp.ToAppPaths());

        await store.SaveAsync(new StoredCredentials { ApiKey = ApiKey, ProxyPassword = ProxyPassword });

        var bytes = await File.ReadAllBytesAsync(temp.File("credentials.dat"));
        var asText = Encoding.Latin1.GetString(bytes);

        Assert.DoesNotContain(ApiKey, asText, StringComparison.Ordinal);
        Assert.DoesNotContain(ProxyPassword, asText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"apiKey\"", asText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UndecryptableFile_IsReportedRatherThanTreatedAsMissing()
    {
        // Stands in for a file copied from another machine or another Windows
        // user: DPAPI CurrentUser keys do not travel.
        using var temp = new TempDirectory();
        await File.WriteAllBytesAsync(temp.File("credentials.dat"), [0x01, 0x02, 0x03, 0x04, 0x05]);

        var result = await new DpapiCredentialStore(temp.ToAppPaths()).LoadAsync();

        Assert.Equal(CredentialLoadStatus.Undecryptable, result.Status);
        Assert.Null(result.Credentials);
    }

    [Fact]
    public async Task EmptyFile_IsReportedAsMissing()
    {
        using var temp = new TempDirectory();
        await File.WriteAllBytesAsync(temp.File("credentials.dat"), []);

        var result = await new DpapiCredentialStore(temp.ToAppPaths()).LoadAsync();

        Assert.Equal(CredentialLoadStatus.Missing, result.Status);
    }

    [Fact]
    public async Task StoringNothing_LoadsAsMissing()
    {
        using var temp = new TempDirectory();
        var store = new DpapiCredentialStore(temp.ToAppPaths());

        await store.SaveAsync(new StoredCredentials());
        var result = await store.LoadAsync();

        Assert.Equal(CredentialLoadStatus.Missing, result.Status);
    }

    [Fact]
    public async Task Overwriting_ReplacesThePreviousKey()
    {
        using var temp = new TempDirectory();
        var store = new DpapiCredentialStore(temp.ToAppPaths());

        await store.SaveAsync(new StoredCredentials { ApiKey = "private_first" });
        await store.SaveAsync(new StoredCredentials { ApiKey = "private_second" });

        var result = await store.LoadAsync();

        Assert.Equal("private_second", result.Credentials!.ApiKey);
        Assert.Equal(["credentials.dat"], temp.FileNames());
    }

    [Fact]
    public async Task Delete_RemovesStoredSecrets()
    {
        using var temp = new TempDirectory();
        var store = new DpapiCredentialStore(temp.ToAppPaths());
        await store.SaveAsync(new StoredCredentials { ApiKey = ApiKey });

        await store.DeleteAsync();
        var result = await store.LoadAsync();

        Assert.Equal(CredentialLoadStatus.Missing, result.Status);
        Assert.Empty(temp.FileNames());
    }

    [Fact]
    public async Task Delete_OnAStoreWithNothingStored_IsHarmless()
    {
        using var temp = new TempDirectory();

        await new DpapiCredentialStore(temp.ToAppPaths()).DeleteAsync();
    }
}
