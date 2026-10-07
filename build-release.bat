@echo off
setlocal
cd /d "%~dp0"
echo [1/2] Building mad-dc-stub...
dotnet publish mad-dc-stub\mad-dc-stub.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 exit /b 1

echo [2/2] Building mad-dc...
dotnet publish mad-dc\mad-dc.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 exit /b 1

echo.
echo Build complete.
echo Main app: mad-dc\bin\Release\net8.0-windows\win-x64\publish\mad-dc.exe
endlocal
