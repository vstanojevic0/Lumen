namespace Lumen.ViewModels;

public sealed class NavItemViewModel
{
    public NavItemViewModel(string title, string iconGlyph, int? count = null, bool isSection = false)
    {
        Title = title;
        IconGlyph = iconGlyph;
        Count = count;
        IsSection = isSection;
    }

    public string Title { get; }
    public string IconGlyph { get; }
    public int? Count { get; }
    public bool IsSection { get; }
    public string Label => Count is null ? Title : $"{Title}  {Count:N0}";
}
