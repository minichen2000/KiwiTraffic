namespace KiwiTraffic.Infrastructure.Storage;

/// <summary>
/// A file that must be readable was not, and the original has been moved to
/// <see cref="BackupPath"/>. Never resolved by silently falling back to
/// defaults - the caller has to tell the user.
/// </summary>
public sealed class CorruptStoreException : Exception
{
    public CorruptStoreException(string path, string backupPath, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Path = path;
        BackupPath = backupPath;
    }

    /// <summary>The file that could not be read (no longer on disk under this name).</summary>
    public string Path { get; }

    /// <summary>Where the unreadable original was preserved.</summary>
    public string BackupPath { get; }
}
