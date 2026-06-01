namespace Lumen.ViewModels;

public sealed class FolderHeaderTimelineItem : TimelineListItemViewModel
{
    public FolderHeaderTimelineItem(string title, string folderPath)
    {
        Title = title;
        FolderPath = folderPath;
    }

    public string Title { get; }
    public string FolderPath { get; }
}
