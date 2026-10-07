using System.Text;

namespace KiwiTraffic.Infrastructure.Storage;

/// <summary>
/// Reads and writes small files safely.
/// </summary>
/// <remarks>
/// Writes go to a temporary file in the same directory, are flushed to disk,
/// and then replace the target in one step, so a crash mid-write can never
/// leave a half-written settings file behind. Text is written as UTF-8
/// <em>without</em> a BOM; reads tolerate a BOM, because a BOM-prefixed file is
/// a common accident on Windows and must not look like corruption.
/// </remarks>
internal static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static Task WriteAsync(string path, string contents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return WriteAsync(path, Utf8NoBom.GetBytes(contents), cancellationToken);
    }

    public static async Task WriteAsync(string path, byte[] contents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Path has no directory: {path}");
        Directory.CreateDirectory(directory);

        // Unique temp name: a fixed ".tmp" would collide with a leftover from a
        // crashed run, or with a second instance writing at the same time.
        var temp = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>Returns <c>null</c> when the file does not exist.</summary>
    /// <remarks>
    /// A leading UTF-8 BOM is stripped rather than kept as a U+FEFF character:
    /// it is a common accident on Windows and must not look like corruption.
    /// (Any other encoding - UTF-16, say - fails the JSON parse instead, which
    /// routes it to the backup path rather than losing the file.)
    /// </remarks>
    public static async Task<string?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        return bytes switch
        {
            null => null,
            [0xEF, 0xBB, 0xBF, .. var rest] => Utf8NoBom.GetString(rest),
            _ => Utf8NoBom.GetString(bytes),
        };
    }

    /// <summary>Returns <c>null</c> when the file does not exist.</summary>
    public static async Task<byte[]?> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
        => File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>
    /// Moves a broken file out of the way so the original content is preserved
    /// and never silently overwritten by defaults.
    /// </summary>
    public static string MoveToBackup(string path)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var backup = $"{path}.corrupt-{stamp}";
        var suffix = 0;
        while (File.Exists(backup))
        {
            suffix++;
            backup = $"{path}.corrupt-{stamp}-{suffix}";
        }

        File.Move(path, backup);
        return backup;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is harmless; it is never read back.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
