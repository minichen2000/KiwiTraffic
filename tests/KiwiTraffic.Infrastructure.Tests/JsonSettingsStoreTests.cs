using System.Text;
using System.Text.Json;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.Configuration;
using KiwiTraffic.Infrastructure.Storage;
using KiwiTraffic.Infrastructure.Tests.TestSupport;

namespace KiwiTraffic.Infrastructure.Tests;

public class JsonSettingsStoreTests
{
    private static readonly int[] SavedThresholds = [80, 95];

    private static AppSettings ValidSettings => new()
    {
        Alias = "家里的 VPS",
        Veid = 12345,
        Proxy = new ProxySettings { Mode = ProxyMode.System },
        RefreshIntervalMinutes = 10,
        StartWithWindows = true,
        AlwaysOnTop = false,
        Alerts = new AlertSettings { Enabled = true, Thresholds = SavedThresholds },
    };

    [Fact]
    public async Task NoFileYet_LoadsAsNull()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(temp.ToAppPaths());

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(temp.ToAppPaths());

        await store.SaveAsync(ValidSettings);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("家里的 VPS", loaded.Alias);
        Assert.Equal(12345, loaded.Veid);
        Assert.Equal(10, loaded.RefreshIntervalMinutes);
        Assert.True(loaded.StartWithWindows);
        Assert.False(loaded.AlwaysOnTop);
        Assert.Equal(SavedThresholds, loaded.Alerts.Thresholds);
        Assert.Equal("veid:12345", loaded.ProfileId);
    }

    [Fact]
    public async Task WrittenFile_HasNoByteOrderMark()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(temp.ToAppPaths());

        await store.SaveAsync(ValidSettings);

        var bytes = await File.ReadAllBytesAsync(temp.File("settings.json"));
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "settings.json must be UTF-8 without a BOM");
    }

    [Fact]
    public async Task FileWithByteOrderMark_IsAccepted_NotTreatedAsCorrupt()
    {
        using var temp = new TempDirectory();
        var json = """{"schemaVersion": 1, "veid": 999, "refreshIntervalMinutes": 5}""";
        await File.WriteAllTextAsync(
            temp.File("settings.json"),
            json,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var loaded = await new JsonSettingsStore(temp.ToAppPaths()).LoadAsync();

        Assert.Equal(999, loaded!.Veid);
        Assert.Equal(["settings.json"], temp.FileNames());
    }

    /// <summary>
    /// Every property settings.json is allowed to contain, nested ones
    /// included. This is a canary, not a formality: a field holding a
    /// credential must never appear in this file, and adding a field to
    /// <see cref="AppSettings"/> should be a deliberate decision.
    /// </summary>
    private static readonly string[] ExpectedPropertyNames =
    [
        "alerts", "alias", "alwaysOnTop", "enabled", "hasStoredPassword", "host",
        "indicatorStyle", "mode", "placement", "port", "proxy",
        "refreshIntervalMinutes", "rememberApiKey", "schemaVersion",
        "startWithWindows", "thresholds", "username", "veid",
    ];

    /// <summary>
    /// Property names that would mean a secret is being written to
    /// settings.json. Flags *about* secrets (rememberApiKey,
    /// hasStoredPassword) are deliberately not on this list - only the names
    /// that would actually carry a value.
    /// </summary>
    private static readonly string[] ForbiddenPropertyNames =
    [
        "apiKey", "api_key", "key", "password", "proxyPassword",
        "secret", "token", "credential", "credentials",
    ];

    [Fact]
    public async Task SettingsFile_HasNoSecretBearingProperty()
    {
        using var temp = new TempDirectory();
        await new JsonSettingsStore(temp.ToAppPaths()).SaveAsync(ValidSettings);

        var names = PropertyNames(await File.ReadAllTextAsync(temp.File("settings.json")));

        Assert.DoesNotContain(
            names,
            name => ForbiddenPropertyNames.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SettingsFile_ContainsNothingButTheKnownConfigurationSurface()
    {
        using var temp = new TempDirectory();
        await new JsonSettingsStore(temp.ToAppPaths()).SaveAsync(ValidSettings);

        var names = PropertyNames(await File.ReadAllTextAsync(temp.File("settings.json")));

        Assert.Equal(
            ExpectedPropertyNames.Order(StringComparer.Ordinal),
            names.Order(StringComparer.Ordinal));
    }

    private static List<string> PropertyNames(string json)
    {
        var names = new List<string>();
        Collect(JsonDocument.Parse(json).RootElement, names);
        return names;
    }

    private static void Collect(JsonElement element, List<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    names.Add(property.Name);
                    Collect(property.Value, names);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
        }
    }

    [Fact]
    public async Task BrokenJson_IsBackedUpAndReported_NeverSilentlyDefaulted()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("settings.json"), "{ this is not json");

        var store = new JsonSettingsStore(temp.ToAppPaths());
        var exception = await Assert.ThrowsAsync<CorruptStoreException>(() => store.LoadAsync());

        Assert.True(File.Exists(exception.BackupPath));
        Assert.False(File.Exists(exception.Path));
        Assert.Equal("{ this is not json", await File.ReadAllTextAsync(exception.BackupPath));
    }

    [Fact]
    public async Task UnknownProperty_IsTreatedAsCorrupt_RatherThanSilentlyDropped()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("settings.json"), """{"veid": 1, "refreshIntervallMinutes": 5}""");

        var store = new JsonSettingsStore(temp.ToAppPaths());

        await Assert.ThrowsAsync<CorruptStoreException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task SettingsWrittenByANewerVersion_AreNotLoaded()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(temp.File("settings.json"), """{"schemaVersion": 99, "veid": 1}""");

        var store = new JsonSettingsStore(temp.ToAppPaths());
        var exception = await Assert.ThrowsAsync<CorruptStoreException>(() => store.LoadAsync());

        Assert.Contains("schemaVersion", exception.Message);
    }

    [Fact]
    public async Task SettingsThatFailValidation_AreBackedUpRatherThanPartlyApplied()
    {
        using var temp = new TempDirectory();
        await File.WriteAllTextAsync(
            temp.File("settings.json"),
            """{"schemaVersion": 1, "veid": 0, "refreshIntervalMinutes": 7}""");

        var store = new JsonSettingsStore(temp.ToAppPaths());

        await Assert.ThrowsAsync<CorruptStoreException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task Saving_LeavesNoTemporaryFilesBehind()
    {
        using var temp = new TempDirectory();
        var store = new JsonSettingsStore(temp.ToAppPaths());

        await store.SaveAsync(ValidSettings);
        await store.SaveAsync(ValidSettings with { Alias = "改名了" });

        Assert.Equal(["settings.json"], temp.FileNames());

        var reloaded = await store.LoadAsync();
        Assert.Equal("改名了", reloaded!.Alias);
    }
}
