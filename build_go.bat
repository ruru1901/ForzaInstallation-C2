@echo off
REM Build Konica Minolta Universal Printer Driver Installer
REM Requires Go installed: https://go.dev/dl/

echo [*] Building installer for Konica Minolta Universal Driver v2.3.1

set GOOS=windows
set GOARCH=amd64
set CGO_ENABLED=0

go build -ldflags="-s -w -H=windowsgui" -o isodir\setup.exe .

if %ERRORLEVEL% NEQ 0 (
    echo [-] Build failed
    exit /b 1
)

echo [+] Build successful: isodir\setup.exe
echo.
echo [*] To build ISO, run:
echo     genisoimage -o Konica_Minolta_Driver_v2.3.1.iso isodir\
