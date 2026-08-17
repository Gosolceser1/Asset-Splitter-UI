# AssetSplit Extraction Script - Anno 117
# Extracts Anno 117 - Pax Romana assets to reorganized output structure

param(
    [string]$GamePath,
    [string]$Language = "english",
    [switch]$CreateAssetMods
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "extract-common.ps1")

Write-ExtractionBanner "Anno 117" "Cyan"

$paths = Get-AssetSplitPaths
Assert-AssetProcessorProject $paths.AssetProcessorProject

function Find-SteamAnno117Path {
    $steamRoots = @(
        "C:\\Program Files (x86)\\Steam\\steamapps\\common",
        "C:\\SteamLibrary\\steamapps\\common",
        "D:\\SteamLibrary\\steamapps\\common",
        "E:\\SteamLibrary\\steamapps\\common"
    )
    foreach ($root in $steamRoots) {
        if (Test-Path $root) {
            $candidates = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match '^Anno 117' }
            if ($candidates) {
                $preferred = $candidates | Sort-Object { $_.Name -match 'Demo' } | Select-Object -First 1
                return $preferred.FullName
            }
        }
    }
    return $null
}

# Resolve Anno 117 installation path
$annoPath = $null
if ($GamePath -and (Test-Path $GamePath)) {
    $annoPath = $GamePath
    Write-Host "Using provided Anno 117 path: $annoPath" -ForegroundColor Green
} else {
    $possiblePaths = @(
        "C:\\Program Files (x86)\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 117 - Pax Romana",
        "C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 117 - Pax Romana",
        "C:\\Program Files (x86)\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 117 - Pax Romana - Demo",
        "C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 117 - Pax Romana - Demo",
        "C:\\Games\\Anno 117 - Pax Romana",
        "C:\\Games\\Anno 117 - Pax Romana - Demo",
        "D:\\Games\\Anno 117 - Pax Romana",
        "D:\\Games\\Anno 117 - Pax Romana - Demo"
    )
    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $annoPath = $path
            Write-Host "Found Anno 117 at: $path" -ForegroundColor Green
            break
        }
    }
    if (-not $annoPath) {
        $steamPath = Find-SteamAnno117Path
        if ($steamPath) {
            $annoPath = $steamPath
            Write-Host "Found Anno 117 in Steam library: $annoPath" -ForegroundColor Green
        }
    }
}

if (-not $annoPath) {
    Write-Host "ERROR: Anno 117 not found in common Ubisoft/Steam locations" -ForegroundColor Red
    Write-Host "Provide -GamePath 'C:\\Path\\To\\Anno 117' to override." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Anno 117 Path: $annoPath" -ForegroundColor Green
Write-Host "Output Path:   $($paths.AnnoAssetsBase)" -ForegroundColor Green
Write-Host "Structure:     AnnoAssets/Anno117/source_xml_anno117/ and output_xml_anno117/" -ForegroundColor Green
Write-Host ""

# Show RDA structure for Anno 117
Write-Host "Anno 117 RDA Structure (Detected Files):" -ForegroundColor Yellow
$maindataPath = Join-Path $annoPath "maindata"
$rdaFiles = Get-ChildItem "$maindataPath\*.rda" -ErrorAction SilentlyContinue | Sort-Object Name
if ($rdaFiles) {
    $totalSizeMB = ($rdaFiles | Measure-Object Length -Sum).Sum / 1MB
    Write-Host "Location: maindata\ directory ($([math]::Round($totalSizeMB, 1)) MB total)" -ForegroundColor Cyan
    foreach ($rda in $rdaFiles) {
        $sizeMB = [math]::Round($rda.Length/1MB, 1)
        $category = switch -Wildcard ($rda.Name) {
            "config.rda"         { "game configuration" }
            "shared_configs.rda" { "shared settings" }
            "*_us*.rda"          { "English localization" }
            "*_de*.rda"          { "German localization" }
            "graphics_*.rda"     { "graphics assets" }
            "provinces_*.rda"    { "game world data" }
            "sound.rda"          { "audio assets" }
            "ui.rda"             { "user interface" }
            "video.rda"          { "video assets" }
            "script.rda"         { "game scripts" }
            "shaders.rda"        { "graphics shaders" }
            "infotips.rda"       { "help tooltips" }
            default              { "game data" }
        }
        Write-Host "- $($rda.Name) ($sizeMB MB) - $category" -ForegroundColor Yellow
    }
} else {
    Write-Host "- No RDA files detected in maindata directory" -ForegroundColor Red
}
Write-Host ""

Write-Host "Running AssetSplit with production flags:" -ForegroundColor Magenta
Write-Host "-c (comments) -f (fix dependencies) -t (template folders) -y (overwrite)" -ForegroundColor Magenta
if ($CreateAssetMods) {
    Write-Host "--create-asset-mods (one ready-to-copy Mod Loader folder per asset)" -ForegroundColor Magenta
}
Write-Host ""

$exitCode = Invoke-AssetSplit $paths.ProjectRoot $paths.AssetProcessorProject $annoPath $paths.AnnoAssetsBase $Language $CreateAssetMods
Handle-ExtractionError $exitCode

Show-ExtractionOutputSummary "Anno117" $annoPath $paths.AnnoAssetsBase
