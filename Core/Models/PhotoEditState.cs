using CommunityToolkit.Mvvm.ComponentModel;

namespace Lumen.Core.Models;

/// <summary>
/// Non-destructive edit parameters for a single photo.
/// </summary>
public partial class PhotoEditState : ObservableObject
{
    public event Action? Changed;

    private bool _suppressChanged;

    [ObservableProperty] private double _exposure;
    [ObservableProperty] private double _contrast;
    [ObservableProperty] private double _highlights;
    [ObservableProperty] private double _shadows;
    [ObservableProperty] private double _whites;
    [ObservableProperty] private double _blacks;
    [ObservableProperty] private double _vibrance;
    [ObservableProperty] private double _saturation;
    [ObservableProperty] private double _straighten;

    partial void OnExposureChanged(double value) => NotifyChanged();
    partial void OnContrastChanged(double value) => NotifyChanged();
    partial void OnHighlightsChanged(double value) => NotifyChanged();
    partial void OnShadowsChanged(double value) => NotifyChanged();
    partial void OnWhitesChanged(double value) => NotifyChanged();
    partial void OnBlacksChanged(double value) => NotifyChanged();
    partial void OnVibranceChanged(double value) => NotifyChanged();
    partial void OnSaturationChanged(double value) => NotifyChanged();
    partial void OnStraightenChanged(double value) => NotifyChanged();

    private void NotifyChanged()
    {
        if (!_suppressChanged)
            Changed?.Invoke();
    }

    public PhotoEditState Clone() => new()
    {
        Exposure = Exposure,
        Contrast = Contrast,
        Highlights = Highlights,
        Shadows = Shadows,
        Whites = Whites,
        Blacks = Blacks,
        Vibrance = Vibrance,
        Saturation = Saturation,
        Straighten = Straighten,
    };

    public void Reset()
    {
        _suppressChanged = true;
        try
        {
            Exposure = 0;
            Contrast = 0;
            Highlights = 0;
            Shadows = 0;
            Whites = 0;
            Blacks = 0;
            Vibrance = 0;
            Saturation = 0;
            Straighten = 0;
        }
        finally
        {
            _suppressChanged = false;
        }

        Changed?.Invoke();
    }
}
