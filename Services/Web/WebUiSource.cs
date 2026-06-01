namespace Lumen.Services.Web;

public static class WebUiSource
{
    public static Uri Resolve()
    {
#if DEBUG
        return new Uri("http://localhost:5173/");
#else
        var index = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (File.Exists(index))
            return new Uri(index);

        return new Uri("http://localhost:5173/");
#endif
    }

    public static string Hint =>
#if DEBUG
        "Start the UI dev server: cd web && npm run dev";
#else
        "Build the UI: cd web && npm run build";
#endif
}
