using System.Collections.ObjectModel;

namespace Lumen.ViewModels;

/// <summary>
/// One month block in the library grid (header + wrapped photo tiles).
/// </summary>
public sealed class MonthSectionViewModel
{
    public MonthSectionViewModel(string headerText, IEnumerable<PhotoTileViewModel> photos)
    {
        HeaderText = headerText;
        foreach (var photo in photos)
            Photos.Add(photo);
    }

    public string HeaderText { get; }
    public ObservableCollection<PhotoTileViewModel> Photos { get; } = new();
}
