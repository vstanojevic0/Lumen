using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.ViewModels;

public partial class DriveToggleItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public DriveToggleItem(string absolutePath, string displayName, bool isSelected)
    {
        AbsolutePath = absolutePath;
        DisplayName = displayName;
        _isSelected = isSelected;
    }

    public string AbsolutePath { get; }
    public string DisplayName { get; }
}
