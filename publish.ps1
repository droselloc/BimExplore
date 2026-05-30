# Genera un ejecutable autocontenido (single-file) para Windows x64.
# Salida: ./publish/BimExplore.exe

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"

$proj = Join-Path $PSScriptRoot "src/BimExplorer.App/BimExplorer.App.csproj"
$outDir = Join-Path $PSScriptRoot $Output

if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

Write-Host "Publicando BIM Explore ($Runtime, $Configuration) ..." -ForegroundColor Cyan

dotnet publish $proj `
    -c $Configuration `
    -r $Runtime `
    -o $outDir `
    /p:PublishSingleFile=true `
    /p:SelfContained=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish fallo con codigo $LASTEXITCODE"
}

$exe = Join-Path $outDir "BimExplore.exe"
if (Test-Path $exe) {
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "OK  ->  $exe  ($sizeMb MB)" -ForegroundColor Green
} else {
    Write-Warning "No se encontro BimExplore.exe en $outDir"
}
