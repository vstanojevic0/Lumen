# Lumen — Web UI u desktop aplikaciji

Desktop aplikacija podrazumevano učitava React UI iz `web/` preko **NativeWebView**, dok skeniranje, indeks i dekodiranje slika ostaju u **.NET**-u.

## Pokretanje (razvoj)

U **dva terminala**:

```bash
# Terminal 1 — Vite dev server (hot reload)
cd web && npm install && npm run dev
```

```bash
# Terminal 2 — Avalonia host
cd /Users/v/Lumen && dotnet run
```

Aplikacija otvara `http://localhost:5173` u WebView-u.

## Klasični Avalonia UI

```bash
dotnet run -- --classic-ui
# ili
LUMEN_CLASSIC_UI=1 dotnet run
```

## Release build

```bash
cd web && npm run build
dotnet publish -c Release -r win-x64 --self-contained -o artifacts/Lumen-win-x64
```

`web/dist` se kopira u `wwwroot/` pored `.exe`.

## Bridge API (JS → C#)

| Metoda | Opis |
|--------|------|
| `ping` | Provera veze |
| `getStatus` | Broj fotografija, status tekst |
| `getGallery` | Sekcije po mesecima + putanje |
| `getThumbnail` | `{ path }` → PNG data URL |
| `getPreview` | Veći preview za edit |
| `rescan` | Ponovo skenira biblioteku |
| `addFolder` | Dijalog za dodavanje foldera |

Događaji: `lumen:status`, `lumen:libraryUpdated`.
