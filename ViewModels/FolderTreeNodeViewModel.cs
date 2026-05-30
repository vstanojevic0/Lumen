using System.Collections.ObjectModel;
using Lumen.Core.Models;

namespace Lumen.ViewModels;

public sealed class FolderTreeNodeViewModel
{
    public FolderTreeNodeViewModel(FolderBrowseNode node)
    {
        AbsolutePath = node.AbsolutePath;
        DisplayName = node.DisplayName;
        PhotoCountDirect = node.PhotoCountDirect;
        foreach (var child in node.Children.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase))
            Children.Add(new FolderTreeNodeViewModel(child));
    }

    public string DisplayName { get; }
    public string AbsolutePath { get; }
    public int PhotoCountDirect { get; }
    public ObservableCollection<FolderTreeNodeViewModel> Children { get; } = new();

    public string Label => PhotoCountDirect > 0
        ? $"{DisplayName}  ({PhotoCountDirect})"
        : DisplayName;
}
