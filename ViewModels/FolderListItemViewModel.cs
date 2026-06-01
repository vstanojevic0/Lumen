namespace Lumen.ViewModels;

public sealed class FolderListItemViewModel
{
    public FolderListItemViewModel(string title, string absolutePath, int photoCount)
    {
        Title = title;
        AbsolutePath = absolutePath;
        PhotoCount = photoCount;
    }

    public string Title { get; }
    public string AbsolutePath { get; }
    public int PhotoCount { get; }

    public string PhotoCountLabel => PhotoCount.ToString("N0");
}
