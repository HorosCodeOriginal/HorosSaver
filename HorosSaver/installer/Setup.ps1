#Requires -Version 5.1
<#
.SYNOPSIS
  Portable HorosSaver setup (fallback when Inno Setup / WiX are unavailable).
.DESCRIPTION
  Copies the self-contained publish folder to %LocalAppData%\Programs\HorosSaver
  and creates Start Menu (+ optional Desktop) shortcuts.
  Does NOT modify user data in %LocalAppData%\HorosCode\HorosSaver (profiles/snapshots).
#>
[CmdletBinding()]
param(
    [string] $InstallDir = '',

    [switch] $DesktopShortcut,

    [switch] $Launch
)

$ErrorActionPreference = 'Stop'

$appName = 'HorosSaver'
$exeName = 'HorosSaver.exe'
$company = 'HorosCode'
$version = '1.0.0'

$sourceDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Join-Path $env:LOCALAPPDATA 'Programs\HorosSaver'
}

$exeSource = Join-Path $sourceDir $exeName
if (-not (Test-Path -LiteralPath $exeSource)) {
    throw "Source executable not found: $exeSource`nRun this script from the extracted publish folder."
}

Write-Host "==> HorosSaver portable setup ($version)" -ForegroundColor Cyan
Write-Host "    Source : $sourceDir"
Write-Host "    Target : $InstallDir"

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

Write-Host "    Copying application files..."
Get-ChildItem -LiteralPath $sourceDir -Force | ForEach-Object {
    $dest = Join-Path $InstallDir $_.Name
    if ($_.PSIsContainer) {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Recurse -Force
    }
    else {
        Copy-Item -LiteralPath $_.FullName -Destination $dest -Force
    }
}

$exeTarget = Join-Path $InstallDir $exeName
$wsh = New-Object -ComObject WScript.Shell

$startMenuRoot = [System.IO.Path]::Combine($env:APPDATA, 'Microsoft', 'Windows', 'Start Menu', 'Programs', $company)
New-Item -ItemType Directory -Path $startMenuRoot -Force | Out-Null
$startMenuLink = Join-Path $startMenuRoot "$appName.lnk"
$shortcut = $wsh.CreateShortcut($startMenuLink)
$shortcut.TargetPath = $exeTarget
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Description = "$company $appName"
$shortcut.Save()
Write-Host "    Start Menu: $startMenuLink"

if ($DesktopShortcut) {
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) "$appName.lnk"
    $desktop = $wsh.CreateShortcut($desktopLink)
    $desktop.TargetPath = $exeTarget
    $desktop.WorkingDirectory = $InstallDir
    $desktop.Description = "$company $appName"
    $desktop.Save()
    Write-Host "    Desktop   : $desktopLink"
}

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "  HorosSaver is installed to: $InstallDir"
Write-Host "  User data (profiles/snapshots) remains in:"
Write-Host "    $([System.IO.Path]::Combine($env:LOCALAPPDATA, 'HorosCode', 'HorosSaver'))"

if ($Launch) {
    Start-Process -FilePath $exeTarget
}
