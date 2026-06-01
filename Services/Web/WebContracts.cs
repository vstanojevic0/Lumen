namespace Lumen.Services.Web;

public sealed record WebPhotoDto(string Path, string Title, bool Favorite);

public sealed record WebGallerySectionDto(string Title, IReadOnlyList<WebPhotoDto> Photos);

public sealed record WebGallerySnapshot(
    int TotalCount,
    string StatusText,
    bool IsBusy,
    IReadOnlyList<WebGallerySectionDto> Sections);

public sealed record WebStatusDto(int TotalCount, string StatusText, bool IsBusy, int FavoriteCount);

public sealed record WebFolderDto(
    string Path,
    string Title,
    int PhotoCount,
    IReadOnlyList<WebFolderDto> Children);

public sealed record WebGalleryRequest(string? FolderPath, bool FavoritesOnly);

public sealed record WebImageDto(string DataUrl);
