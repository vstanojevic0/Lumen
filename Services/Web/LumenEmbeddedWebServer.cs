using System.Net;
using System.Net.Sockets;

namespace Lumen.Services.Web;

/// <summary>
/// Serves bundled <c>wwwroot</c> on loopback so WebView2 loads the SPA without Vite or file:// quirks.
/// </summary>
public sealed class LumenEmbeddedWebServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _root;
    private readonly CancellationTokenSource _shutdown = new();

    private LumenEmbeddedWebServer(HttpListener listener, string root, Uri baseUri)
    {
        _listener = listener;
        _root = root;
        BaseUri = baseUri;
    }

    public Uri BaseUri { get; }

    public static LumenEmbeddedWebServer? TryStart()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var index = Path.Combine(root, "index.html");
        if (!File.Exists(index))
            return null;

        var port = GetFreeTcpPort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var server = new LumenEmbeddedWebServer(listener, root, new Uri($"http://127.0.0.1:{port}/"));
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
            var path = context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
            if (string.IsNullOrEmpty(path))
                path = "index.html";

            var localPath = Path.GetFullPath(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!localPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(localPath))
            {
                localPath = Path.Combine(_root, "index.html");
            }

            var bytes = await File.ReadAllBytesAsync(localPath).ConfigureAwait(false);
            context.Response.ContentType = GetContentType(localPath);
            context.Response.Headers.Add("Cache-Control", "no-cache");
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            context.Response.StatusCode = 200;
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
