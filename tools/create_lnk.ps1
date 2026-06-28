param(
    [string]$ExePath = "isodir\setup.exe",
    [string]$OutputPath = "Konica_Minolta_Printer_Driver_Update.lnk",
    [string]$IconPath = "isodir\KM_Icon.ico",
    [string]$Arguments = "",
    [string]$Description = "Konica Minolta Universal Printer Driver v2.3.1",
    [switch]$HideWindow
)

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($OutputPath)

$shortcut.TargetPath = $ExePath
$shortcut.Description = $Description

if ($Arguments -ne "") {
    $shortcut.Arguments = $Arguments
}

if ($IconPath -ne "" -and (Test-Path $IconPath)) {
    $shortcut.IconLocation = $IconPath
} else {
    $shortcut.IconLocation = "$ExePath,0"
}

if ($HideWindow) {
    $shortcut.WindowStyle = 7
}

$shortcut.WorkingDirectory = Split-Path $ExePath -Parent

$shortcut.Save()

Write-Host "[+] LNK created: $OutputPath"
Write-Host "    Target: $ExePath"
Write-Host "    Description: $Description"
Write-Host "    WindowStyle: $($shortcut.WindowStyle)"

$bytes = [System.IO.File]::ReadAllBytes($OutputPath)
Write-Host "    Size: $($bytes.Length) bytes"
