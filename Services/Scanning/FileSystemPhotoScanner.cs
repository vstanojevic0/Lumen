using Lumen.Core.Abstractions;
using Lumen.Core.Models;
using Lumen.Services.Catalog;

namespace Lumen.Services.Scanning;

/// <summary>
/// Recursive filesystem walk with extension filtering and path exclusions.
/// </summary>
public sealed class FileSystemPhotoScanner : ILibraryScanner
{
    public async IAsyncEnumerable<PhotoEntry> ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<int>? progress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var count = 0;

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
                continue;

            foreach (var path in EnumeratePhotosRecursive(root, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!seenPaths.Add(path))
                    continue;

                count++;
                if (count % 200 == 0)
                    progress?.Report(count);

                var entry = TryCreateEntry(path);
                if (entry is null)
                    continue;

                yield return entry;
            }
        }

        progress?.Report(count);
    }

    private static PhotoEntry? TryCreateEntry(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return null;

            if (!ScanPathExclusions.ShouldIncludePhotoFile(path, info.Length))
                return null;

            // TODO: replace with EXIF-based capture time (e.g. MetadataExtractor / Magick.NET).
            var taken = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            return new PhotoEntry(path, taken, info.Length, null, null);
        }
        catch (Exception ex) when (IsBenignAccessFailure(ex))
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumeratePhotosRecursive(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(Path.TrimEndingDirectorySeparator(root));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            if (ScanPathExclusions.ShouldSkipDirectory(directory))
                continue;

            foreach (var file in TryEnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (PhotoFileExtensions.IsPhotoFile(file))
                    yield return file;
            }

            foreach (var subdir in TryEnumerateDirectories(directory))
            {
                if (!ScanPathExclusions.ShouldSkipDirectory(subdir))
                    pending.Push(subdir);
            }
        }
    }

    private static IEnumerable<string> TryEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch (Exception ex) when (IsBenignAccessFailure(ex))
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> TryEnumerateDirectories(string directory)
    {
        try
        {
            return Directory.EnumerateDirectories(directory);
        }
        catch (Exception ex) when (IsBenignAccessFailure(ex))
        {
            return Array.Empty<string>();
        }
    }

    internal static bool IsBenignAccessFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException or DirectoryNotFoundException)
                return true;

            if (current is IOException io &&
                (io.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                 io.Message.Contains("Trash", StringComparison.OrdinalIgnoreCase) ||
                 io.Message.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase)))
                return true;

            if (current.Message.Contains("denied", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("Trash", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
