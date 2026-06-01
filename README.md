# Lumen

Desktop photo library for Windows and macOS (Avalonia + .NET 10). Browse photos by folder, edit with basic adjustments, histogram, crop, and export.

## Requirements (development)

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run locally

```bash
dotnet run
```

## Web UI prototype (React)

Interactive editor mock with live slider preview, presets, crop overlay, and filmstrip:

```bash
cd web && npm install && npm run dev
```

See [web/README.md](web/README.md).

## Windows download (pre-built)

**[Download Lumen-win-x64.zip (latest release)](https://github.com/vstanojevic0/Lumen/releases/latest)** — unzip and run `Lumen.exe`.

## Windows build (portable folder)

On Windows (PowerShell):

```powershell
.\scripts\publish-windows.ps1
```

On macOS/Linux (cross-publish):

```bash
./scripts/publish-windows.sh
```

Output: `artifacts/Lumen-win-x64/Lumen.exe` — copy the whole `Lumen-win-x64` folder (or the zip) to a Windows PC. No separate .NET install required.

## macOS build

```bash
dotnet publish -c Release -r osx-arm64 --self-contained true -o artifacts/Lumen-osx-arm64
# Intel Mac: osx-x64
```

## Library locations

- **Windows:** first-run wizard or **Library locations…** in the sidebar.
- **macOS:** Pictures, Desktop, and Downloads are indexed automatically on first launch (for testing). Use **Add folder…** if macOS blocks access to some paths.
