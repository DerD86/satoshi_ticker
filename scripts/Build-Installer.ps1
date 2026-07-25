$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishScript = Join-Path $PSScriptRoot "Publish-Portable.ps1"
$installerScript = Join-Path $projectRoot "installer\SatoshiTicker.iss"

& $publishScript

$possibleCompilerPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$compiler = $possibleCompilerPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "Inno Setup 6 wurde nicht gefunden. Installiere Inno Setup 6 und starte dieses Skript erneut."
}

Write-Host "Erstelle Installer ..." -ForegroundColor Cyan
& $compiler $installerScript

Write-Host ""
Write-Host "Installer fertig:" -ForegroundColor Green
Write-Host "$projectRoot\dist\installer\SatoshiTicker-Setup-1.0.0.exe"
