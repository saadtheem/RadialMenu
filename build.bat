@echo off
REM Framework-dependent build: needs the .NET 8 SDK (or Desktop Runtime to run), downloads nothing extra.
dotnet publish -c Release -r win-x64 --self-contained false -o dist
echo.
echo Done. Run dist\RadialMenu.exe
pause
