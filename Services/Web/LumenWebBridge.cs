using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Lumen.ViewModels;

namespace Lumen.Services.Web;

/// <summary>
/// JSON-RPC bridge between the React UI and <see cref="LibraryViewModel"/>.
/// </summary>
public sealed class LumenWebBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private NativeWebView? _webView;
    private LibraryViewModel? _vm;

    public void Attach(NativeWebView webView, LibraryViewModel viewModel)
    {
        Detach();

        _webView = webView;
        _vm = viewModel;

        webView.WebMessageReceived += OnWebMessageReceived;
        viewModel.LibraryUpdated += OnLibraryUpdated;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public async Task InitializeAsync()
    {
        await InjectHostScriptAsync().ConfigureAwait(true);
        await PushStatusAsync().ConfigureAwait(true);
        await PushLibraryUpdatedAsync().ConfigureAwait(true);
    }

    public void Detach()
    {
        if (_webView is not null)
            _webView.WebMessageReceived -= OnWebMessageReceived;

        if (_vm is not null)
        {
            _vm.LibraryUpdated -= OnLibraryUpdated;
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _webView = null;
        _vm = null;
    }

    private void OnLibraryUpdated() => _ = PushLibraryUpdatedAsync();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LibraryViewModel.IsBusy)
            or nameof(LibraryViewModel.StatusText)
            or nameof(LibraryViewModel.TotalPhotoCount))
        {
            _ = PushStatusAsync();
        }
    }

    private async void OnWebMessageReceived(object? sender, WebMessageReceivedEventArgs e)
    {
        if (_vm is null || _webView is null || string.IsNullOrWhiteSpace(e.Body))
            return;

        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(e.Body, JsonOptions);
        }
        catch
        {
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Method))
            return;

        try
        {
            var result = await HandleAsync(request.Method, request.Params).ConfigureAwait(true);
            await SendResponseAsync(request.Id, result, null).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await SendResponseAsync(request.Id, null, ex.Message).ConfigureAwait(true);
        }
    }

    private async Task<object?> HandleAsync(string method, JsonElement? parameters)
    {
        var vm = _vm ?? throw new InvalidOperationException("View model is not attached.");

        return method switch
        {
            "ping" => new { ok = true, host = "lumen" },
            "getStatus" => vm.GetWebStatus(),
            "getFolders" => vm.GetWebFolderTree(),
            "getGallery" => GetGallery(vm, parameters),
            "getThumbnail" => ImageUrlResponse(parameters, preview: false),
            "getPreview" => ImageUrlResponse(parameters, preview: true),
            "setFavorite" => SetFavorite(vm, parameters),
            "rescan" => await RescanAsync(vm).ConfigureAwait(true),
            "addFolder" => await AddFolderAsync(vm).ConfigureAwait(true),
            _ => throw new NotSupportedException($"Unknown method: {method}")
        };
    }

    private static async Task<object> RescanAsync(LibraryViewModel vm)
    {
        await vm.RequestRescanAsync().ConfigureAwait(true);
        return new { ok = true };
    }

    private static async Task<object> AddFolderAsync(LibraryViewModel vm)
    {
        await vm.RequestAddFolderAsync().ConfigureAwait(true);
        return new { ok = true };
    }

    private static WebGallerySnapshot GetGallery(LibraryViewModel vm, JsonElement? parameters)
    {
        var (folderPath, favoritesOnly) = ParseGalleryRequest(parameters);
        return vm.GetWebGallerySnapshot(folderPath, favoritesOnly);
    }

    private static (string? FolderPath, bool FavoritesOnly) ParseGalleryRequest(JsonElement? parameters)
    {
        if (parameters is not JsonElement el)
            return (null, false);

        string? folderPath = null;
        if (el.TryGetProperty("folderPath", out var folderProp) &&
            folderProp.ValueKind == JsonValueKind.String)
        {
            folderPath = folderProp.GetString();
        }

        var favoritesOnly = el.TryGetProperty("favoritesOnly", out var favProp) &&
                            favProp.ValueKind is JsonValueKind.True;

        return (folderPath, favoritesOnly);
    }

    private static object SetFavorite(LibraryViewModel vm, JsonElement? parameters)
    {
        if (parameters is not JsonElement el ||
            !el.TryGetProperty("path", out var pathProp) ||
            !el.TryGetProperty("favorite", out var favProp))
        {
            throw new ArgumentException("path and favorite are required");
        }

        var path = pathProp.GetString();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required");

        var favorite = favProp.ValueKind is JsonValueKind.True;
        vm.SetPhotoFavorite(path, favorite);
        return new { ok = true, favorite };
    }

    private static object ImageUrlResponse(JsonElement? parameters, bool preview)
    {
        if (parameters is not { } el || !el.TryGetProperty("path", out var pathProp))
            throw new ArgumentException("path is required");

        var path = pathProp.GetString();
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required");

        var url = preview ? WebUiSource.PreviewUrl(path) : WebUiSource.ThumbUrl(path);
        if (url is null)
            throw new InvalidOperationException("Media server is not running.");

        return new WebImageDto(url);
    }

    private async Task InjectHostScriptAsync()
    {
        if (_webView is null)
            return;

        const string script = """
            (function () {
              if (window.__lumenHost && window.__lumenHost.isAvailable) return;
              window.__lumenHostPending = window.__lumenHostPending || new Map();
              window.__lumenHost = {
                isAvailable: true,
                call(method, params) {
                  return new Promise((resolve, reject) => {
                    const id = String(Date.now()) + '-' + Math.random().toString(16).slice(2);
                    window.__lumenHostPending.set(id, { resolve, reject });
                    const payload = JSON.stringify({ id, method, params: params ?? null });
                    if (typeof invokeCSharpAction === 'function') {
                      invokeCSharpAction(payload);
                      return;
                    }
                    reject(new Error('Lumen host bridge is not ready (invokeCSharpAction missing).'));
                  });
                },
                _dispatch(message) {
                  const pending = window.__lumenHostPending.get(message.id);
                  if (!pending) return;
                  window.__lumenHostPending.delete(message.id);
                  if (message.error) pending.reject(new Error(message.error));
                  else pending.resolve(message.result);
                },
                _event(name, payload) {
                  window.dispatchEvent(new CustomEvent('lumen:' + name, { detail: payload }));
                }
              };
            })();
            """;

        await _webView.InvokeScript(script).ConfigureAwait(true);

        var mediaBase = WebUiSource.MediaBaseUri?.ToString().TrimEnd('/');
        if (!string.IsNullOrEmpty(mediaBase))
        {
            var json = JsonSerializer.Serialize(mediaBase, JsonOptions);
            await _webView.InvokeScript($"window.__lumenMediaBase = {json}").ConfigureAwait(true);
        }

        await _webView.InvokeScript("window.dispatchEvent(new CustomEvent('lumen:hostReady'))").ConfigureAwait(true);
    }

    private async Task SendResponseAsync(string id, object? result, string? error)
    {
        if (_webView is null)
            return;

        var payload = JsonSerializer.Serialize(new BridgeResponse(id, result, error), JsonOptions);
        await _webView.InvokeScript($"window.__lumenHost._dispatch({payload})").ConfigureAwait(true);
    }

    private async Task PushStatusAsync()
    {
        if (_webView is null || _vm is null)
            return;

        var status = _vm.GetWebStatus();
        var json = JsonSerializer.Serialize(status, JsonOptions);
        await _webView.InvokeScript($"window.__lumenHost._event('status', {json})").ConfigureAwait(true);
    }

    private async Task PushLibraryUpdatedAsync()
    {
        if (_webView is null || _vm is null)
            return;

        await _webView.InvokeScript("window.__lumenHost._event('libraryUpdated', {})").ConfigureAwait(true);
    }

    private sealed record BridgeRequest(string Id, string Method, JsonElement? Params);

    private sealed record BridgeResponse(
        string Id,
        object? Result,
        [property: JsonPropertyName("error")] string? Error);
}
