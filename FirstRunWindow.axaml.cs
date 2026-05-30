using Avalonia.Controls;
using Lumen.Services.Settings;
using Lumen.ViewModels;

namespace Lumen;

public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
        : this(new JsonAppSettingsStore(), LibrarySetupMode.FirstRun)
    {
    }

    public FirstRunWindow(IAppSettingsStore store, LibrarySetupMode mode)
    {
        InitializeComponent();
        DataContext = new FirstRunViewModel(store, this, mode);
    }
}
