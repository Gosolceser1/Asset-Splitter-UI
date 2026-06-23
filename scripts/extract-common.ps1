# Shared helpers for Anno extraction scripts (anno117-extract.ps1, anno1800-extract.ps1)
# Source this with: . (Join-Path $PSScriptRoot "extract-common.ps1")

function Get-AssetSplitPaths {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $AssetProcessorProject = Join-Path $ProjectRoot "src\AssetSplitter\AssetSplitter.csproj"
    $DocumentsPath = [Environment]::GetFolderPath("MyDocuments")
    $OutputBase = Join-Path $DocumentsPath "AssetSplit_Output"
    $AnnoAssetsBase = Join-Path $OutputBase "AnnoAssets"
    return @{
        ProjectRoot = $ProjectRoot
        AssetProcessorProject = $AssetProcessorProject
        OutputBase = $OutputBase
        AnnoAssetsBase = $AnnoAssetsBase
    }
}

function Invoke-AssetSplit($ProjectRoot, $AssetProcessorProject, $GamePath, $OutputBase, $Language, $CreateAssetMods) {
    $assetSplitArgs = @("$GamePath", "$OutputBase", $Language, "-c", "-f", "-t", "-y")
    if ($CreateAssetMods) { $assetSplitArgs += "--create-asset-mods" }

    Push-Location $ProjectRoot
    try {
        & dotnet run --project $AssetProcessorProject -- @assetSplitArgs
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    return $exitCode
}

function Show-ExtractionOutputSummary($GameKey, $OutputFolder, $AnnoAssetsBase) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "    Extraction Complete!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $gameFolder = Join-Path $AnnoAssetsBase $GameKey
    $sourceKey = "source_xml_" + $GameKey.ToLower()
    $outputKey = "output_xml_" + $GameKey.ToLower()

    $sourceXmlPath = Join-Path $gameFolder $sourceKey
    $outputXmlPath = Join-Path $gameFolder $outputKey
    $assetModsPath = Join-Path $gameFolder "mods"

    if (Test-Path $gameFolder) {
        Write-Host "Files created in: $gameFolder" -ForegroundColor Green
        Write-Host ""
        Write-Host "Folder Structure:" -ForegroundColor Cyan
        Write-Host "+-- $GameKey/" -ForegroundColor Cyan
        if (Test-Path $sourceXmlPath) {
            Write-Host "|   +-- $sourceKey/  (raw game data from RDA)" -ForegroundColor Green
        }
        if (Test-Path $outputXmlPath) {
            Write-Host "|   +-- $outputKey/  (processed ModOp files)" -ForegroundColor Green
        }
        if (Test-Path $assetModsPath) {
            Write-Host "|   +-- mods/       (one Mod Loader mod per asset)" -ForegroundColor Green
        }
    } else {
        Write-Host "Warning: Output folder not found at expected location" -ForegroundColor Yellow
        Write-Host "Expected: $gameFolder" -ForegroundColor Yellow
    }

    Write-Host ""
    Read-Host "Press Enter to exit"
}

function Write-ExtractionBanner($GameTitle, $Color) {
    Write-Host "========================================" -ForegroundColor $Color
    Write-Host "  AssetSplit - $GameTitle Extraction" -ForegroundColor $Color
    Write-Host "========================================" -ForegroundColor $Color
    Write-Host ""
}

function Assert-AssetProcessorProject($ProjectPath) {
    if (-not (Test-Path $ProjectPath)) {
        Write-Host "[ERROR] AssetProcessor project not found: $ProjectPath" -ForegroundColor Red
        exit 1
    }
}

function Handle-ExtractionError($exitCode) {
    if ($exitCode -ne 0) {
        Write-Host ""
        Write-Host "[ERROR] AssetSplit failed with exit code $exitCode" -ForegroundColor Red
        exit $exitCode
    }
}
