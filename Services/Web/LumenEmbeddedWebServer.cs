using System.Net;
using System.Net.Sockets;

namespace Lumen.Services.Web;

/// <summary>
/// Loopback server: bundled SPA (<c>wwwroot</c>) and <c>/media/*</c> image bytes (no base64 in JS bridge).
/// </summary>
public sealed class LumenEmbeddedWebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string? _root;
    private readonly WebMediaHandler _media;
    private readonly CancellationTokenSource _shutdown = new();

    private LumenEmbeddedWebServer(HttpListener listener, string? wwwroot, WebMediaHandler media, Uri baseUri)
    {
        _listener = listener;
        _root = wwwroot;
        _media = media;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public static LumenEmbeddedWebServer? TryStart(WebMediaHandler media)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var hasUi = File.Exists(Path.Combine(root, "index.html"));
        var wwwroot = hasUi ? root : null;

        var port = GetFreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var server = new LumenEmbeddedWebServer(listener, wwwroot, media, new Uri($"http://127.0.0.1:{port}/"));
        _ = server.RunAsync(server._shutdown.Token);
        return server;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            AddCors(context);

            var path = context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
            if (path.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
            {
                await ServeMediaAsync(context, path).ConfigureAwait(false);
                return;
            }

            if (_root is null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (string.IsNullOrEmpty(path))
                path = "index.html";

            var localPath = Path.GetFullPath(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!localPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(localPath))
                localPath = Path.Combine(_root, "index.html");

            await WriteFileAsync(context, localPath, GetContentType(localPath)).ConfigureAwait(false);
        }
        catch
        {
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task ServeMediaAsync(HttpListenerContext context, string path)
    {
        var query = context.Request.Url?.Query ?? string.Empty;
        var photoPath = ParseQueryParam(query, "p");
        photoPath = WebMediaHandler.DecodePathParameter(photoPath);

        if (string.IsNullOrWhiteSpace(photoPath))
        {
            context.Response.StatusCode = 400;
            return;
        }

        var maxEdge = path.StartsWith("media/preview", StringComparison.OrdinalIgnoreCase)
            ? WebMediaHandler.PreviewMaxEdge
            : WebMediaHandler.ThumbMaxEdge;

        var bytes = await _media.GetPngAsync(photoPath, maxEdge, _shutdown.Token).ConfigureAwait(false);
        if (bytes is null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        context.Response.ContentType = "image/png";
        context.Response.Headers.Add("Cache-Control", "private, max-age=3600");
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.StatusCode = 200;
    }

    private static string? ParseQueryParam(string query, string name)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }

        return null;
    }

    private static async Task WriteFileAsync(HttpListenerContext context, string localPath, string contentType)
    {
        var bytes = await File.ReadAllBytesAsync(localPath).ConfigureAwait(false);
        context.Response.ContentType = contentType;
        context.Response.Headers.Add("Cache-Control", "no-cache");
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.StatusCode = 200;
    }

    private static void AddCors(HttpListenerContext context)
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _listener.Stop();
        }
        catch
        {
        }

        _listener.Close();
        _shutdown.Dispose();
    }
}
