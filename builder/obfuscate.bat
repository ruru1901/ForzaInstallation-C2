@echo off
echo [*] ForzaInstallation Obfuscator (ConfuserEx)
echo.

if "%1"=="" (
    echo Usage: obfuscate.bat ^<ForzaInstallation.exe^>
    exit /b 1
)

set INPUT=%1
set OUTPUT=%~n1_obfuscated.exe

if not exist "ConfuserEx\Confuser.CLI.exe" (
    if exist "%ProgramFiles%\ConfuserEx\Confuser.CLI.exe" (
        set CONFUSER="%ProgramFiles%\ConfuserEx\Confuser.CLI.exe"
    ) else (
        echo [-] ConfuserEx not found.
        echo [*] Download from: https://github.com/mkaring/ConfuserEx/releases
        echo [*] Place Confuser.CLI.exe in .\ConfuserEx\ folder
        echo [*] Skipping obfuscation...
        copy "%INPUT%" "%OUTPUT%" >nul
        echo [+] Copied un-obfuscated to %OUTPUT%
        exit /b 0
    )
)

echo [*] Running ConfuserEx...
%CONFUSER% -n ..\confuser.cfg -in "%INPUT%" -out "%OUTPUT%"

if %ERRORLEVEL% EQU 0 (
    echo [+] Obfuscation complete: %OUTPUT%
) else (
    echo [-] Obfuscation failed, using un-obfuscated copy
    copy "%INPUT%" "%OUTPUT%" >nul
)
