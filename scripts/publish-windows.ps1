# Portable Windows x64 build — includes bundled web UI (no Node/Vite on target PC).
# Requires on build machine: .NET 10 SDK + Node.js (npm).
# Output: artifacts/Lumen-win-x64/Lumen.exe (+ wwwroot folder)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$Out = Join-Path $Root "artifacts" "Lumen-win-x64"
if (Test-Path $Out) { Remove-Item -Recurse -Force $Out }
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null

Write-Host "Building web UI..."
Set-Location (Join-Path $Root "web")
if (-not (Test-Path "node_modules")) { npm ci }
npm run build
Set-Location $Root

if (-not (Test-Path (Join-Path $Root "web\dist\index.html"))) {
    throw "web/dist/index.html missing after npm run build"
}

Write-Host "Publishing to $Out ..."
dotnet publish "$Root\Lumen.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o $Out

$wwwroot = Join-Path $Out "wwwroot\index.html"
if (-not (Test-Path $wwwroot)) {
    throw "Publish succeeded but wwwroot\index.html is missing."
}

$zip = Join-Path $Root "artifacts" "Lumen-win-x64.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path $Out -DestinationPath $zip -Force

Write-Host ""
Write-Host "Done."
Write-Host "  Run:  $Out\Lumen.exe"
Write-Host "  Zip:  $zip"
Write-Host "Copy the whole Lumen-win-x64 folder (or zip) to Windows — no extra install steps."
