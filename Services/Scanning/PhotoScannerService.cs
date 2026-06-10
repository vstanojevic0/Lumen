using Lumen.Services.Catalog;

namespace Lumen.Services.Scanning;

/// <summary>
/// Enumerates supported image files under a folder root without loading metadata.
/// </summary>
public sealed class PhotoScannerService
{
    public IEnumerable<string> EnumeratePhotoPaths(string root, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
            yield break;

        root = Path.TrimEndingDirectorySeparator(root);
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();

            if (ScanPathExclusions.ShouldSkipDirectory(directory))
                continue;

            foreach (var file in TryEnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!PhotoFileExtensions.IsPhotoFile(file))
                    continue;

                if (!TryGetFileLength(file, out var length))
                    continue;

                if (!ScanPathExclusions.ShouldIncludePhotoFile(file, length))
                    continue;

                yield return Path.GetFullPath(file);
            }

            foreach (var subdir in TryEnumerateDirectories(directory))
            {
                if (!ScanPathExclusions.ShouldSkipDirectory(subdir))
                    pending.Push(subdir);
            }
        }
    }

    private static bool TryGetFileLength(string file, out long length)
    {
        length = 0;
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists)
                return false;

            length = info.Length;
            return true;
        }
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
        {
            return false;
        }
    }

    private static IEnumerable<string> TryEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory);
        }
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
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
        catch (Exception ex) when (FileSystemPhotoScanner.IsBenignAccessFailure(ex))
        {
            return Array.Empty<string>();
        }
    }
}
