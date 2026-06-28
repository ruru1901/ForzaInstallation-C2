param(
    [string]$ExePath = "isodir\setup.exe",
    [string]$OutputPath = "Konica_Minolta_Universal_Printer_Driver_v2.3.1.lnk",
    [string]$IconPath = "isodir\KM_Icon.ico",
    [string]$Description = "Konica Minolta Universal Printer Driver v2.3.1",
    [string]$WorkingDir = ""
)

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Resolve-Path $OutputPath -ErrorAction SilentlyContinue).Path ?? (Join-Path (Get-Location) $OutputPath))

$shortcut.TargetPath = $ExePath
$shortcut.Description = $Description
$shortcut.WindowStyle = 1

if ($IconPath -ne "" -and (Test-Path $IconPath)) {
    $shortcut.IconLocation = $IconPath
} else {
    # Try to extract icon from the exe itself
    $shortcut.IconLocation = "$ExePath,0"
}

if ($WorkingDir -ne "") {
    $shortcut.WorkingDirectory = $WorkingDir
} else {
    $shortcut.WorkingDirectory = Split-Path $ExePath -Parent
}

$shortcut.Save()
Write-Host "[+] LNK created: $OutputPath"
Write-Host "    Target: $ExePath"
Write-Host "    Description: $Description"
$bytes = [System.IO.File]::ReadAllBytes($OutputPath)
Write-Host "    Size: $($bytes.Length) bytes"
