@echo off
setlocal
cd /d "%~dp0.."
if not exist "artifacts\Lumen-win-x64" mkdir "artifacts\Lumen-win-x64"
echo Publishing...
dotnet publish Lumen.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=true -o "artifacts\Lumen-win-x64"
if errorlevel 1 exit /b 1
echo.
echo Done. Run: artifacts\Lumen-win-x64\Lumen.exe
echo Zip artifacts\Lumen-win-x64 to copy to another PC.
pause
