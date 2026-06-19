@echo off
echo [*] ForzaInstallation Code Signer
echo.

if "%1"=="" (
    echo Usage: sign.bat ^<exe_path^> ^<cert_path^> [password]
    exit /b 1
)

set EXE=%1
set CERT=%2
set PASS=%3

if not exist "%EXE%" (
    echo [-] File not found: %EXE%
    exit /b 1
)
if not exist "%CERT%" (
    echo [-] Certificate not found: %CERT%
    exit /b 1
)

echo [*] Signing with timestamp...

signtool sign /fd SHA256 /f "%CERT%" %PASS: =/p %^>NUL ^
    /tr http://timestamp.digicert.com /td SHA256 ^
    "%EXE%"

if %ERRORLEVEL% EQU 0 (
    echo [+] Signed successfully
    signtool verify /v /pa "%EXE%" 2>&1 | findstr /i "certificate chain" >nul
    if !ERRORLEVEL! EQU 0 (
        echo [+] Signature verified
    )
) else (
    echo [-] Signing failed
)
