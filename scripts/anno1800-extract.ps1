# AssetSplit Extraction Script - Anno 1800
# Extracts Anno 1800 assets to reorganized output structure

param(
    [string]$GamePath,
    [string]$Language = "english",
    [switch]$CreateAssetMods
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "extract-common.ps1")

Write-ExtractionBanner "Anno 1800" "Yellow"

$paths = Get-AssetSplitPaths
Assert-AssetProcessorProject $paths.AssetProcessorProject

# Resolve Anno 1800 installation path
$annoPath = $null
if ($GamePath -and (Test-Path $GamePath)) {
    $annoPath = $GamePath
    Write-Host "Using provided Anno 1800 path: $annoPath" -ForegroundColor Green
} else {
    $possiblePaths = @(
        "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800",
        "C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 1800",
        "C:\Games\Anno 1800",
        "D:\Games\Anno 1800"
    )
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $annoPath = $path
            Write-Host "Found Anno 1800 at: $path" -ForegroundColor Green
            break
        }
    }
}

if (-not $annoPath) {
    Write-Host ""
    Write-Host "[ERROR] GAME NOT DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Anno 1800 installation not found in common locations." -ForegroundColor Yellow
    Write-Host "Use -GamePath 'C:\Path\To\Anno 1800' to override." -ForegroundColor White
    Write-Host ""
    Write-Host "Press Enter to exit..." -ForegroundColor Gray
    Read-Host
    exit 1
}

Write-Host "Game Path: $annoPath" -ForegroundColor Green
Write-Host "Output Path: $($paths.AnnoAssetsBase)" -ForegroundColor Green
Write-Host "Structure: AnnoAssets/Anno1800/source_xml_anno1800/ and output_xml_anno1800/" -ForegroundColor Green
Write-Host ""
Write-Host "Launching AssetSplit (RDA Explorer + Asset Splitter)..." -ForegroundColor Magenta
Write-Host ""

$exitCode = Invoke-AssetSplit $paths.ProjectRoot $paths.AssetProcessorProject $annoPath $paths.AnnoAssetsBase $Language $CreateAssetMods
Handle-ExtractionError $exitCode

Show-ExtractionOutputSummary "Anno1800" $annoPath $paths.AnnoAssetsBase
