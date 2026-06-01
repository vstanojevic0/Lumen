using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.ViewModels;

/// <summary>
/// Picasa-style folder album card (cover thumbnail + name + count).
/// </summary>
public partial class FolderAlbumViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private Bitmap? _coverThumbnail;

    public FolderAlbumViewModel(string title, string folderPath, string coverPhotoPath, int photoCount)
    {
        Title = title;
        FolderPath = folderPath;
        CoverPhotoPath = coverPhotoPath;
        PhotoCount = photoCount;
    }

    public string Title { get; }
    public string FolderPath { get; }
    public string CoverPhotoPath { get; }
    public int PhotoCount { get; }

    public string PhotoCountLabel => PhotoCount.ToString("N0");

    public void Dispose()
    {
        CoverThumbnail?.Dispose();
        CoverThumbnail = null;
    }
}
