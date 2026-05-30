using System.Collections.ObjectModel;

namespace Lumen.ViewModels;

public sealed class FolderGroupViewModel : IDisposable
{
    public FolderGroupViewModel(string title, string folderPath)
    {
        Title = title;
        FolderPath = folderPath;
    }

    public string Title { get; }
    public string FolderPath { get; }
    public ObservableCollection<PhotoTileViewModel> Tiles { get; } = new();

    public void Dispose()
    {
        foreach (var t in Tiles)
            t.Dispose();
        Tiles.Clear();
    }
}
