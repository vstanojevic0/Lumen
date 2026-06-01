#!/usr/bin/env bash
# Cross-publish portable Windows x64 from macOS/Linux (includes bundled web UI).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/Lumen-win-x64"

echo "Building web UI..."
cd "$ROOT/web"
if [[ ! -d node_modules ]]; then npm ci; fi
npm run build
cd "$ROOT"

if [[ ! -f "$ROOT/web/dist/index.html" ]]; then
  echo "error: web/dist/index.html missing" >&2
  exit 1
fi

rm -rf "$OUT"
mkdir -p "$(dirname "$OUT")"
echo "Publishing to $OUT ..."

dotnet publish "$ROOT/Lumen.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o "$OUT"

if [[ ! -f "$OUT/wwwroot/index.html" ]]; then
  echo "error: wwwroot/index.html missing in publish output" >&2
  exit 1
fi

ZIP="$ROOT/artifacts/Lumen-win-x64.zip"
rm -f "$ZIP"
(cd "$(dirname "$OUT")" && zip -rq "$ZIP" "$(basename "$OUT")")

echo ""
echo "Done."
echo "  Folder: $OUT"
echo "  Zip:    $ZIP"
echo "  On Windows: unzip and run Lumen.exe (no Node or dotnet needed)."
