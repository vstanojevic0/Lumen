namespace Lumen.ViewModels;

/// <summary>
/// Picasa-style library navigation: chronological library, folder album grid, single-folder view.
/// </summary>
public enum LibraryViewMode
{
    /// <summary>All photos grouped by capture date (Picasa Library / lightbox timeline).</summary>
    Library,

    /// <summary>Grid of folder album cards (Picasa Folders collection / Folder Manager).</summary>
    FolderAlbums,

    /// <summary>Photos inside one folder, grouped by date.</summary>
    FolderPhotos
}
