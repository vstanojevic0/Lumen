# Lumen — Web UI u desktop aplikaciji

## Windows (samo pokreni .exe)

1. Preuzmi **[Lumen-win-x64.zip](https://github.com/vstanojevic0/Lumen/releases/latest)** (ili build lokalno, vidi ispod).
2. Raspakuj ceo folder `Lumen-win-x64`.
3. Pokreni **`Lumen.exe`**.

Ni Node, ni Vite, ni `dotnet run` — UI je ubačen u folder (`wwwroot` pored exe).

Potrebno na Windowsu: **WebView2** (uglavnom već na Win 11; na Win 10 [runtime](https://developer.microsoft.com/microsoft-edge/webview2/) ako se prozor ne otvori).

## Razvoj (macOS / dev)

Dva terminala:

```bash
cd web && npm run dev
```

```bash
dotnet run
```

Debug koristi `http://localhost:5173`. Release koristi ugrađeni loopback server + `wwwroot`.

## Build Windows paketa

**Na Windowsu (PowerShell):**

```powershell
.\scripts\publish-windows.ps1
```

**Sa Mac/Linux (cross-publish):**

```bash
./scripts/publish-windows.sh
```

Zahteva **Node.js** i **.NET 10 SDK** samo na mašini koja **build-uje**, ne na PC gde koristiš app.

Izlaz: `artifacts/Lumen-win-x64/Lumen.exe` + `wwwroot/` + `artifacts/Lumen-win-x64.zip`

## Klasični Avalonia UI

```bash
dotnet run -- --classic-ui
```
