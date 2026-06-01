using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.ViewModels;

public partial class PhotoTileViewModel : TimelineListItemViewModel, IDisposable
{
    [ObservableProperty] private Bitmap? _thumbnail;

    public PhotoTileViewModel(string absolutePath, string caption)
    {
        AbsolutePath = absolutePath;
        Caption = caption;
    }

    public string AbsolutePath { get; }
    public string Caption { get; }

    [ObservableProperty] private bool _isSelected;

    public void Dispose()
    {
        Thumbnail?.Dispose();
        Thumbnail = null;
    }
}
