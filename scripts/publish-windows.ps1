# Builds a portable Windows x64 folder (no separate .NET install needed on the target PC).
# Requires: .NET 10 SDK on the machine where you run this script.
# Output: repo/artifacts/Lumen-win-x64/

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$Out = Join-Path $Root "artifacts" "Lumen-win-x64"
New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null

Write-Host "Publishing to $Out ..."

dotnet publish "$Root/Lumen.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Out

Write-Host "Done. Run: $Out\Lumen.exe"
Write-Host "Zip the 'Lumen-win-x64' folder to copy to another PC."
