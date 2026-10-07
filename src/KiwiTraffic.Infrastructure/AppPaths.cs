namespace KiwiTraffic.Infrastructure;

/// <summary>
/// Where the application keeps its local data. Defaults to
/// <c>%LOCALAPPDATA%\KiwiTraffic</c>; tests pass their own root.
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "KiwiTraffic";

    public AppPaths(string? rootDirectory = null)
    {
        Root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
    }

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>DPAPI-encrypted container for the API key and proxy password.</summary>
    public string CredentialsFile => Path.Combine(Root, "credentials.dat");

    public string CacheFile => Path.Combine(Root, "cache.json");

    public string NotificationsFile => Path.Combine(Root, "notifications.json");

    public string LogDirectory => Path.Combine(Root, "logs");

    public void EnsureCreated() => Directory.CreateDirectory(Root);

    /// <summary>The data directory for the current user, as shown to the user.</summary>
    public static string DefaultRoot => new AppPaths().Root;
}
