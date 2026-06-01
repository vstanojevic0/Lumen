#!/usr/bin/env bash# Cross-publish portable Windows x64 build from macOS/Linux.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/Lumen-win-x64"

mkdir -p "$(dirname "$OUT")"
echo "Publishing to $OUT ..."

dotnet publish "$ROOT/Lumen.csproj" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT"

ZIP="$ROOT/artifacts/Lumen-win-x64.zip"
rm -f "$ZIP"
(cd "$(dirname "$OUT")" && zip -r "$ZIP" "$(basename "$OUT")")

echo "Done."
echo "  Folder: $OUT"
echo "  Zip:    $ZIP"
echo "  Run on Windows: Lumen-win-x64\\Lumen.exe"
