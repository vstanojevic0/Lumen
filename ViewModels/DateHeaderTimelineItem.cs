namespace Lumen.ViewModels;

public sealed class DateHeaderTimelineItem : TimelineListItemViewModel
{
    public DateHeaderTimelineItem(string title, int photoCount)
    {
        Title = title;
        PhotoCount = photoCount;
    }

    public string Title { get; }
    public int PhotoCount { get; }
    public string HeaderText => $"{Title} ({PhotoCount:N0} Photos)";
}
