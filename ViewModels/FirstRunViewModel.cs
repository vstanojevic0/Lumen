using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Services.Library;
using Lumen.Services.Settings;

namespace Lumen.ViewModels;

public partial class FirstRunViewModel : ObservableObject
{
    private readonly IAppSettingsStore _store;
    private readonly Window _window;

    public LibrarySetupMode Mode { get; }

    public ObservableCollection<DriveToggleItem> Locations { get; } = new();

    [ObservableProperty] private string _hintText =
        "Choose which locations to index. You can change this later from the toolbar.";

    public FirstRunViewModel(IAppSettingsStore store, Window window, LibrarySetupMode mode)
    {
        _store = store;
        _window = window;
        Mode = mode;

        var suggestions = LibraryLocationCatalog.GetSuggestedScanCandidates().ToList();
        var current = store.Load().ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var c in suggestions)
        {
            var path = Path.TrimEndingDirectorySeparator(c.AbsolutePath);
            var selected = mode == LibrarySetupMode.FirstRun || current.Contains(path);
            Locations.Add(new DriveToggleItem(path, c.DisplayName, selected));
        }

        if (suggestions.Count == 0)
            HintText = "No fixed drives were detected (non-Windows), or no drives are ready. Use \"Add folder…\" from the main window to grant access to a folder.";
    }

    private IReadOnlyList<string> PreservedCustomRoots()
    {
        var suggested = LibraryLocationCatalog.GetSuggestedScanCandidates()
            .Select(c => Path.TrimEndingDirectorySeparator(c.AbsolutePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return _store.Load().ScanRoots
            .Select(Path.TrimEndingDirectorySeparator)
            .Where(r => !suggested.Contains(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Persist(IReadOnlyList<string> selectedCandidateRoots)
    {
        var merged = PreservedCustomRoots()
            .Concat(selectedCandidateRoots.Select(Path.TrimEndingDirectorySeparator))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var s = _store.Load();
        s.FirstRunCompleted = true;
        s.ScanRoots = merged;
        _store.Save(s);
        _window.Close(true);
    }

    [RelayCommand]
    private void Continue()
    {
        var selected = Locations.Where(l => l.IsSelected).Select(l => l.AbsolutePath).ToList();
        if (selected.Count == 0)
        {
            HintText = "Select at least one location, or use \"Use all suggested\".";
            return;
        }

        Persist(selected);
    }

    [RelayCommand]
    private void UseDefaults()
    {
        var all = LibraryLocationCatalog.GetSuggestedScanCandidates()
            .Select(c => Path.TrimEndingDirectorySeparator(c.AbsolutePath))
            .ToList();

        if (all.Count == 0 && PreservedCustomRoots().Count == 0)
        {
            HintText = "No suggested locations are available on this machine. Close this window and use \"Add folder…\" from the main toolbar.";
            return;
        }

        Persist(all);
    }
}
