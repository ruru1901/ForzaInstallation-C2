@echo off
REM Build for Windows x64 — requires Go installed
REM Cross-compile from Linux/macOS or native on Windows

REM Install Go if not present: https://go.dev/dl/
REM Then run this script

echo [*] Building konica_minolta_printer-driver-update.exe

set GOOS=windows
set GOARCH=amd64
set CGO_ENABLED=0

go build -ldflags="-s -w -H=windowsgui" -o konica_minolta_printer-driver-update.exe main.go

if %ERRORLEVEL% NEQ 0 (
    echo [-] Build failed
    exit /b 1
)

echo [+] Build successful: konica_minolta_printer-driver-update.exe
echo.
echo [*] To generate .lnk, run:
echo     powershell -ExecutionPolicy Bypass -File tools\gen_lnk.ps1
