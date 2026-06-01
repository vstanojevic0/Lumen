# Lumen Web UI

Interactive React + Tailwind prototype of the Lumen photo library and editor (modern Picasa-style layout).

## Run

```bash
cd web
npm install
npm run dev
```

Open http://localhost:5173

## Features

- **Left sidebar** — navigation, albums, folders, storage bar
- **Top toolbar** — search, Library/Edit mode, undo/redo, zoom, export
- **Center workspace** — live CSS-filter preview, crop grid, filmstrip, ratings
- **Right panel** — collapsible edit sections with working sliders, presets, crop controls

All edit values are stored in React state with undo/redo history. Sliders update the preview instantly via CSS filters.

## Stack

- React 19 + Vite 6
- Tailwind CSS 4
- lucide-react icons

This UI is separate from the Avalonia desktop app in the repo root (`dotnet run`).
