namespace KiwiTraffic.Infrastructure.Tests.TestSupport;

/// <summary>
/// A throwaway directory for tests that touch the file system, so no test ever
/// writes near the real <c>%LOCALAPPDATA%\KiwiTraffic</c>.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "KiwiTraffic.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public AppPaths ToAppPaths() => new(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string[] FileNames() =>
        [.. Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).OfType<string>()];

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is harmless.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
