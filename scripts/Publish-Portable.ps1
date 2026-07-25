$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $projectRoot "dist\portable"

Write-Host "Erstelle eigenständige Windows-x64-Version ..." -ForegroundColor Cyan

dotnet publish "$projectRoot\SatoshiTicker.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output "$outputPath"

Write-Host ""
Write-Host "Fertig:" -ForegroundColor Green
Write-Host "$outputPath\SatoshiTicker.exe"
