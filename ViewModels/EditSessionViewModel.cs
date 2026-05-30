using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lumen.Core.Models;
using Lumen.Services.Imaging;
using SkiaSharp;

namespace Lumen.ViewModels;

public partial class EditSessionViewModel : ObservableObject, IDisposable
{
    private SKBitmap? _sourceBitmap;
    private CancellationTokenSource? _renderCts;
    private CancellationTokenSource? _loadCts;
    private bool _isLoadingPhoto;
    private bool _syncingFilmstrip;
    private bool _cropCommitted;
    private int _loadGeneration;

    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private Bitmap? _displayImage;
    [ObservableProperty] private bool _isCropMode;
    [ObservableProperty] private bool _isInfoPanel = true;
    [ObservableProperty] private string _cameraInfo = "—";
    [ObservableProperty] private string _captureInfo = "—";
    [ObservableProperty] private string _dimensionsInfo = "—";
    [ObservableProperty] private string _loadError = string.Empty;
    [ObservableProperty] private bool _isLoadingPreview;
    [ObservableProperty] private ImageHistogram? _histogram;
    [ObservableProperty] private double _previewZoom = 1;
    [ObservableProperty] private double _previewPanX;
    [ObservableProperty] private double _previewPanY;

    public PhotoEditState Edits { get; } = new();

    public EditSessionViewModel()
    {
        Edits.Changed += () =>
        {
            if (!_isLoadingPhoto)
                _ = RenderPreviewAsync();
        };
    }

    public CropRect CropRect { get; } = new(0, 0, 1, 1);

    public double CropX
    {
        get => CropRect.X;
        set { CropRect.X = value; CropRect.Clamp(); SyncCropProperties(); NotifyCropChanged(); }
    }

    public double CropY
    {
        get => CropRect.Y;
        set { CropRect.Y = value; CropRect.Clamp(); SyncCropProperties(); NotifyCropChanged(); }
    }

    public double CropWidth
    {
        get => CropRect.Width;
        set { CropRect.Width = value; CropRect.Clamp(); SyncCropProperties(); NotifyCropChanged(); }
    }

    public double CropHeight
    {
        get => CropRect.Height;
        set { CropRect.Height = value; CropRect.Clamp(); SyncCropProperties(); NotifyCropChanged(); }
    }

    private void SyncCropProperties()
    {
        OnPropertyChanged(nameof(CropX));
        OnPropertyChanged(nameof(CropY));
        OnPropertyChanged(nameof(CropWidth));
        OnPropertyChanged(nameof(CropHeight));
    }

    public ObservableCollection<PhotoTileViewModel> Filmstrip { get; } = new();

    [ObservableProperty] private PhotoTileViewModel? _selectedFilmstripItem;

    partial void OnSelectedFilmstripItemChanged(PhotoTileViewModel? value)
    {
        if (_syncingFilmstrip || value is null)
            return;

        if (!string.Equals(value.AbsolutePath, FilePath, StringComparison.OrdinalIgnoreCase))
            _ = LoadPhotoAsync(value.AbsolutePath, value.Caption);
    }

    public async Task LoadPhotoAsync(string absolutePath, string caption)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var loadToken = _loadCts.Token;

        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = null;

        _isLoadingPhoto = true;
        IsLoadingPreview = true;
        LoadError = string.Empty;
        FilePath = absolutePath;
        FileName = caption;

        ClearDisplayImage();
        Histogram = null;
        ResetPreviewTransform();

        try
        {
            await ShowQuickPreviewAsync(absolutePath, generation, loadToken).ConfigureAwait(false);
            if (loadToken.IsCancellationRequested || generation != _loadGeneration)
                return;

            _sourceBitmap?.Dispose();
            _sourceBitmap = null;

            var loaded = await Task.Run(() => PhotoEditPipeline.LoadSource(absolutePath), loadToken)
                .ConfigureAwait(false);

            if (loadToken.IsCancellationRequested || generation != _loadGeneration)
            {
                loaded?.Dispose();
                return;
            }

            if (loaded is null)
            {
                await SetLoadErrorAsync("Could not decode this file.", generation).ConfigureAwait(false);
                return;
            }

            _sourceBitmap = loaded;
            Edits.Reset();
            CropRect.X = 0;
            CropRect.Y = 0;
            CropRect.Width = 1;
            CropRect.Height = 1;
            _cropCommitted = false;
            IsCropMode = false;
            SyncCropProperties();
            DimensionsInfo = $"{loaded.Width} × {loaded.Height}";
            UpdateMetadata(absolutePath);

            await RenderPreviewAsync(loadToken, generation).ConfigureAwait(false);
        }
        finally
        {
            if (generation == _loadGeneration)
                IsLoadingPreview = false;

            _isLoadingPhoto = false;
        }
    }

    private async Task ShowQuickPreviewAsync(string absolutePath, int generation, CancellationToken cancellationToken)
    {
        var pngBytes = await Task.Run(
            () => ImageLoader.TryEncodeThumbnailBytes(absolutePath, 2048),
            cancellationToken).ConfigureAwait(false);

        if (pngBytes is null || cancellationToken.IsCancellationRequested || generation != _loadGeneration)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested || generation != _loadGeneration)
                return;

            var bitmap = ImageLoader.CreateBitmapFromPngBytes(pngBytes)
                         ?? ImageLoader.TryDecodeWithAvalonia(absolutePath, 2048);

            if (bitmap is null)
                return;

            ReplaceDisplayImage(bitmap);
        });
    }

    public void SetFilmstrip(IEnumerable<PhotoTileViewModel> items, PhotoTileViewModel current)
    {
        _syncingFilmstrip = true;
        try
        {
            Filmstrip.Clear();
            foreach (var item in items)
            {
                item.IsSelected = string.Equals(item.AbsolutePath, current.AbsolutePath, StringComparison.OrdinalIgnoreCase);
                Filmstrip.Add(item);
            }

            SelectedFilmstripItem = Filmstrip.FirstOrDefault(i => i.IsSelected) ?? current;
        }
        finally
        {
            _syncingFilmstrip = false;
        }
    }

    partial void OnIsCropModeChanged(bool value)
    {
        if (!value)
        {
            _cropCommitted = true;
            _ = RenderPreviewAsync();
        }
        else
        {
            _ = RenderPreviewAsync();
        }
    }

    [RelayCommand]
    private void ToggleCropMode() => IsCropMode = !IsCropMode;

    [RelayCommand]
    private void TogglePanel() => IsInfoPanel = !IsInfoPanel;

    [RelayCommand]
    private void ResetEdits()
    {
        Edits.Reset();
        CropRect.X = 0;
        CropRect.Y = 0;
        CropRect.Width = 1;
        CropRect.Height = 1;
        _cropCommitted = false;
        SyncCropProperties();
        _ = RenderPreviewAsync();
    }

    public void NotifyCropChanged()
    {
        if (!IsCropMode)
            _ = RenderPreviewAsync();
    }

    public void ResetPreviewTransform()
    {
        PreviewZoom = 1;
        PreviewPanX = 0;
        PreviewPanY = 0;
    }

    public int? GetFilmstripIndex()
    {
        for (var i = 0; i < Filmstrip.Count; i++)
        {
            if (Filmstrip[i].IsSelected)
                return i;
        }

        return null;
    }

    public bool TrySelectFilmstripIndex(int index)
    {
        if (index < 0 || index >= Filmstrip.Count)
            return false;

        var tile = Filmstrip[index];
        _syncingFilmstrip = true;
        try
        {
            foreach (var item in Filmstrip)
                item.IsSelected = ReferenceEquals(item, tile);
            SelectedFilmstripItem = tile;
        }
        finally
        {
            _syncingFilmstrip = false;
        }

        if (!string.Equals(tile.AbsolutePath, FilePath, StringComparison.OrdinalIgnoreCase))
            _ = LoadPhotoAsync(tile.AbsolutePath, tile.Caption);

        return true;
    }

    public async Task RenderPreviewAsync(CancellationToken cancellationToken = default, int? loadGeneration = null)
    {
        if (_sourceBitmap is null)
            return;

        var ownsCts = cancellationToken == default;
        CancellationTokenSource? localCts = null;
        if (ownsCts)
        {
            _renderCts?.Cancel();
            _renderCts?.Dispose();
            localCts = new CancellationTokenSource();
            _renderCts = localCts;
            cancellationToken = localCts.Token;
        }

        var path = FilePath;
        var edits = Edits.Clone();
        var crop = ResolveCropForPreview();
        var source = _sourceBitmap;
        var generation = loadGeneration ?? _loadGeneration;

        byte[]? pngBytes = null;
        ImageHistogram? histogram = null;
        try
        {
            (pngBytes, histogram) = await Task.Run(() =>
            {
                using var rendered = PhotoEditPipeline.Render(source, edits, crop);
                if (rendered is null)
                    return ((byte[]?)null, (ImageHistogram?)null);

                var hist = HistogramBuilder.Compute(rendered);
                var png = PhotoEditPipeline.EncodePng(rendered);
                return (png, hist);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested || generation != _loadGeneration)
            return;

        if (pngBytes is null)
        {
            (pngBytes, histogram) = await Task.Run(() =>
            {
                var bytes = ImageLoader.TryEncodeThumbnailBytes(path, 4096);
                if (bytes is null)
                    return ((byte[]?)null, (ImageHistogram?)null);

                using var decoded = SKBitmap.Decode(bytes);
                var hist = decoded is null ? null : HistogramBuilder.Compute(decoded);
                return (bytes, hist);
            }, cancellationToken).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested || generation != _loadGeneration)
            return;

        if (pngBytes is null)
        {
            await SetLoadErrorAsync("Could not render preview.", generation).ConfigureAwait(false);
            return;
        }

        var histSnapshot = histogram;
        await ApplyPreviewOnUiThreadAsync(cancellationToken, generation, pngBytes, path, histSnapshot)
            .ConfigureAwait(false);
    }

    private async Task ApplyPreviewOnUiThreadAsync(
        CancellationToken cancellationToken,
        int generation,
        byte[] pngBytes,
        string path,
        ImageHistogram? histSnapshot)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested || generation != _loadGeneration)
                return;

            var bitmap = ImageLoader.CreateBitmapFromPngBytes(pngBytes)
                         ?? ImageLoader.TryDecodeWithAvalonia(path, 4096);

            if (bitmap is null)
            {
                LoadError = "Could not display this file.";
                return;
            }

            ReplaceDisplayImage(bitmap);
            LoadError = string.Empty;
            Histogram = histSnapshot;
        });
    }

    private async Task SetLoadErrorAsync(string message, int generation)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != _loadGeneration)
                return;

            LoadError = message;
        });
    }

    private void ClearDisplayImage()
    {
        var old = DisplayImage;
        DisplayImage = null;
        old?.Dispose();
    }

    private void ReplaceDisplayImage(Bitmap bitmap)
    {
        var old = DisplayImage;
        DisplayImage = bitmap;
        old?.Dispose();
    }

    private CropRect? ResolveCropForPreview()
    {
        if (IsCropMode || !_cropCommitted)
            return null;

        return CropRect.Clone();
    }

    public async Task ExportAsync(string destinationPath)
    {
        if (_sourceBitmap is null)
            return;

        var ext = Path.GetExtension(destinationPath).ToLowerInvariant();
        var crop = _cropCommitted ? CropRect.Clone() : null;

        await Task.Run(() =>
        {
            using var rendered = PhotoEditPipeline.Render(_sourceBitmap, Edits, crop);
            if (rendered is null)
                return;

            using var image = SKImage.FromBitmap(rendered);
            var format = ext is ".jpg" or ".jpeg" ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
            using var data = image.Encode(format, 95);
            if (data is null)
                return;

            using var fs = File.Create(destinationPath);
            data.SaveTo(fs);
        }).ConfigureAwait(true);
    }

    private void UpdateMetadata(string path)
    {
        try
        {
            var info = new FileInfo(path);
            CaptureInfo = info.LastWriteTimeUtc.ToLocalTime().ToString("MMM dd, yyyy  h:mm tt");
            CameraInfo = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        }
        catch
        {
            CaptureInfo = CameraInfo = "—";
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _sourceBitmap?.Dispose();
        DisplayImage?.Dispose();
    }
}
