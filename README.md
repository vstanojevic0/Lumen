# Lumen

Desktop photo library — **React UI** hosted in Avalonia WebView, **C#** for scanning, catalog, images, and settings.

## Architecture

```
web/ (React)  ←→  LumenWebBridge  ←→  LibraryViewModel
                                      →  Scanner, Index, ImageLoader, Settings
```

## Run (development)

```bash
cd web && npm install && npm run dev   # terminal 1
cd .. && dotnet run                    # terminal 2
```

Debug loads UI from `http://localhost:5173`. Release bundles `web/dist` into the app.

## Windows (portable)

```powershell
.\scripts\publish-windows.ps1
```

Unzip `artifacts/Lumen-win-x64` and run `Lumen.exe`.

## Requirements

- .NET 10 SDK
- Node.js (build / dev only)
