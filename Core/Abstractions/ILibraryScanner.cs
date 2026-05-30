using Lumen.Core.Models;

namespace Lumen.Core.Abstractions;

/// <summary>
/// Walks configured roots, filters supported extensions, does not persist state by itself.
/// </summary>
public interface ILibraryScanner
{
    /// <param name="roots">Npr. sve fiksne diskove ili korisnički izabrani folderi.</param>
    /// <param name="progress">Opciono za progress bar tokom prvog skeniranja.</param>
    /// <param name="cancellationToken">Prekid dugog skeniranja.</param>
    IAsyncEnumerable<PhotoEntry> ScanAsync(
        IReadOnlyList<string> roots,
        IProgress<int>? progress,
        CancellationToken cancellationToken = default);
}
