namespace Lumen.Services.Web;

public static class WebUiSource
{
    /// <summary>Loopback server started at app launch (Release with bundled wwwroot).</summary>
    public static Uri? EmbeddedBaseUri { get; set; }

    public static Uri Resolve()
    {
#if DEBUG
        return new Uri("http://localhost:5173/");
#else
        if (EmbeddedBaseUri is not null)
            return EmbeddedBaseUri;

        var index = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(index))
            return new Uri(index);

        throw new InvalidOperationException(
            "Bundled web UI is missing. Rebuild with: dotnet publish -c Release -r win-x64");
#endif
    }

    public static bool HasBundledUi =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html"));

    public static string Hint =>
#if DEBUG
        "Start the UI dev server: cd web && npm run dev";
#else
        "Bundled UI missing. Run scripts/publish-windows.ps1 on a machine with Node.js and .NET SDK.";
#endif
}
