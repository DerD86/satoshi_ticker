$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $projectRoot "dist\framework-dependent"

Write-Host "Erstelle kleine Windows-x64-Version ..." -ForegroundColor Cyan

dotnet publish "$projectRoot\SatoshiTicker.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output "$outputPath"

Write-Host ""
Write-Host "Fertig:" -ForegroundColor Green
Write-Host "$outputPath\SatoshiTicker.exe"
Write-Host "Hinweis: Auf dem Ziel-PC muss die .NET 8 Desktop Runtime installiert sein."
