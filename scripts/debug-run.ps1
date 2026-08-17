# debug-run.ps1
# PSScriptAnalyzer may report false positives: "Missing closing }" and "Missing closing )" (script runs correctly).
#
# Runs AssetProcessor.exe with the -d (DebugMode) flag enabled.
# Produces verbose [DEBUG] output showing every pipeline decision:
#   - Config paths resolved (production vs dev fallback)
#   - Template/fixlist loading and caching
#   - Per-asset template matches, comment insertions, folder moves
#   - BaseAssetGUID resolution chains
#   - VectorElement cleanup counts
#   - Regional ingredient assignments
#
# Output goes to artifacts/debug-output/ inside the repo (gitignored).
# Use -Guid to debug a single specific asset without running the full pipeline.
#
# USAGE:
#   .\scripts\debug-run.ps1
#   .\scripts\debug-run.ps1 -Game anno117
#   .\scripts\debug-run.ps1 -GamePath "D:\Games\Anno 1800" -Language english
#   .\scripts\debug-run.ps1 -Guid 1234567 -AddComments
#   .\scripts\debug-run.ps1 -AddComments -FixDependencies -TemplateFolders
#
# FLAGS (all optional; -Overwrite is always on):
#   -Game              "anno1800" or "anno117" -- skip auto-detect
#   -GamePath          Override game installation path
    #   -OutputPath        Override output directory (default: artifacts/debug-output/)
#   -Language          Game language for GUID comments (default: english)
#   -Guid              Extract only one asset by GUID number
#   -AddComments       Pass -c (translated GUID comments)
#   -FixDependencies   Pass -f (resolve BaseAssetGUID chains)
#   -TemplateFolders   Pass -t (organize by template folder)
#   -NoModOpsWrap      Pass --no-modops-wrap (raw <Asset> XML)
#   -SplitTemplates    Pass --split-templates (one XML per template)
#   -CreateAssetMods   Pass --create-asset-mods (one Mod Loader folder per asset)
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'warnCount', Justification='Used in Run stats and summary')]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'errorCount', Justification='Used in Run stats and summary')]
param(
    [string]$Game       = "",
    [string]$GamePath   = "",
    [string]$OutputPath = "",
    [string]$Language   = "english",
    [string]$Guid       = "",
    [switch]$AddComments,
    [switch]$FixDependencies,
    [switch]$TemplateFolders,
    [switch]$NoModOpsWrap,
    [switch]$SplitTemplates,
    [switch]$CreateAssetMods
)

$ErrorActionPreference = "Stop"
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$AssetProcessorProject = Join-Path $ProjectRoot "src\AssetSplitter\AssetSplitter.csproj"

if (-not (Test-Path $AssetProcessorProject)) {
    Write-Host "  [ERROR] AssetProcessor project not found:" -ForegroundColor Red
    Write-Host "  $AssetProcessorProject" -ForegroundColor DarkGray
    Write-Host ""
    exit 1
}

# ============================================================
#  HEADER
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "   Asset Splitter  |  DEBUG MODE  (-d)" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host ""

# ============================================================
#  OUTPUT DIRECTORY
# ============================================================
if (-not $OutputPath) {
    $OutputPath = Join-Path $ProjectRoot "artifacts/debug-output"
}
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# ============================================================
#  GAME PATH RESOLUTION
# ============================================================
if (-not $GamePath) {

    $anno1800Candidates = @(
        'C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800',
        'C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 1800',
        'C:\Games\Anno 1800',
        'D:\Games\Anno 1800',
        'C:\Program Files (x86)\Steam\steamapps\common\Anno 1800',
        'C:\Program Files\Steam\steamapps\common\Anno 1800',
        'D:\SteamLibrary\steamapps\common\Anno 1800',
        'E:\SteamLibrary\steamapps\common\Anno 1800'
    )

    $anno117Candidates = @(
        'C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana',
        'C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana',
        'C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana - Demo',
        'C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 117 - Pax Romana - Demo',
        'C:\Games\Anno 117 - Pax Romana',
        'D:\Games\Anno 117 - Pax Romana',
        'C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana',
        'D:\SteamLibrary\steamapps\common\Anno 117 - Pax Romana'
    )

    $detected1800 = $anno1800Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    $detected117  = $anno117Candidates  | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($Game -eq "anno1800" -and $detected1800) {
        $GamePath = $detected1800
    } elseif ($Game -eq "anno117" -and $detected117) {
        $GamePath = $detected117
    } elseif ($detected1800 -and $detected117) {

        Write-Host "  Both games detected. Select one to debug:" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "    [1]  Anno 1800" -ForegroundColor Cyan
        Write-Host "         $detected1800" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "    [2]  Anno 117 - Pax Romana" -ForegroundColor Cyan
        Write-Host "         $detected117" -ForegroundColor DarkGray
        Write-Host ""
        $choice = Read-Host "  Enter choice (1 or 2)"
        $GamePath = if ($choice -eq "2") { $detected117 } else { $detected1800 }
        Write-Host ""

    } elseif ($detected1800) {
        $GamePath = $detected1800
    } elseif ($detected117) {
        $GamePath = $detected117
    } else {
        Write-Host "  [ERROR] No Anno game found in common locations." -ForegroundColor Red
        Write-Host "  Use -GamePath 'C:\Path\To\Anno 1800' to override." -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

if (-not (Test-Path $GamePath)) {
    Write-Host "  [ERROR] Game path does not exist:" -ForegroundColor Red
    Write-Host "  $GamePath" -ForegroundColor DarkGray
    Write-Host ""
    exit 1
}

# ============================================================
#  INTERACTIVE SETUP  (shown when no flags were passed)
# ============================================================
$anyFlagPassed = $AddComments -or $FixDependencies -or $TemplateFolders -or $NoModOpsWrap -or $SplitTemplates -or $CreateAssetMods

if (-not $anyFlagPassed) {
    Write-Host "  Feature selection  (comma-separated numbers, or Enter to skip):" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "    [1]  GUID comments      (-c)   Add translated GUID names as XML comments" -ForegroundColor White
    Write-Host "    [2]  Fix dependencies   (-f)   Resolve BaseAssetGUID chains" -ForegroundColor White
    Write-Host "    [3]  Template folders   (-t)   Sort output into template subfolders" -ForegroundColor White
    Write-Host "    [4]  No ModOps wrap           Raw <Asset> XML  (skip <ModOps> wrapper)" -ForegroundColor White
    Write-Host "    [5]  Split templates          One XML file per template" -ForegroundColor White
    Write-Host "    [6]  Create asset mods        One ready-to-copy Mod Loader folder per asset" -ForegroundColor White
    Write-Host ""
    $featureInput = (Read-Host "  Features").Trim()
    $selected     = @($featureInput -split '[,\s]+' | Where-Object { $_ } | ForEach-Object { $_.Trim() })
    if ($selected -contains "1") { $AddComments    = $true }
    if ($selected -contains "2") { $FixDependencies= $true }
    if ($selected -contains "3") { $TemplateFolders= $true }
    if ($selected -contains "4") { $NoModOpsWrap   = $true }
    if ($selected -contains "5") { $SplitTemplates = $true }
    if ($selected -contains "6") { $CreateAssetMods = $true }
    Write-Host ""

    # Language
    if ($Language -eq "english") {
        Write-Host "  Language  (english / german / french / spanish / italian / polish / russian / chinese / japanese / korean)" -ForegroundColor Cyan
        $langInput = (Read-Host "  Language  [english]").Trim()
        if ($langInput) { $Language = $langInput }
        Write-Host ""
    }

    # Single GUID
    if (-not $Guid) {
        Write-Host "  Single GUID  (enter a GUID number to process only that asset, or Enter to process all)" -ForegroundColor Cyan
        $guidInput = (Read-Host "  GUID  [all]").Trim()
        if ($guidInput) { $Guid = $guidInput }
        Write-Host ""
    }
}

# ============================================================
#  BUILD FLAG LIST
# ============================================================
$extraFlags = @("-d", "-y")   # -d = DebugMode, -y = overwrite always

if ($AddComments)    { $extraFlags += "-c" }
if ($FixDependencies){ $extraFlags += "-f" }
if ($TemplateFolders -or $CreateAssetMods){ $extraFlags += "-t" }
if ($NoModOpsWrap -and -not $CreateAssetMods) { $extraFlags += "--no-modops-wrap" }
if ($SplitTemplates) { $extraFlags += "--split-templates" }
if ($CreateAssetMods){ $extraFlags += "--create-asset-mods" }
if ($Guid)           { $extraFlags += "-g:$Guid" }

$activeFeatures = @()
if ($AddComments)    { $activeFeatures += "GUID comments (-c)" }
if ($FixDependencies){ $activeFeatures += "fix dependencies (-f)" }
if ($TemplateFolders){ $activeFeatures += "template folders (-t)" }
if ($NoModOpsWrap -and -not $CreateAssetMods) { $activeFeatures += "no ModOps wrap" }
if ($SplitTemplates) { $activeFeatures += "split templates" }
if ($CreateAssetMods){ $activeFeatures += "create asset mods" }
if ($Guid)           { $activeFeatures += "single GUID: $Guid" }

# ============================================================
#  PARAMETER SUMMARY
# ============================================================
Write-Host "  Game path  :  $GamePath" -ForegroundColor White
Write-Host "  Output     :  $OutputPath" -ForegroundColor White
Write-Host "  Language   :  $Language" -ForegroundColor White
Write-Host "  Flags      :  $($extraFlags -join ' ')" -ForegroundColor Magenta

if ($activeFeatures.Count -gt 0) {
    Write-Host "  Features   :  $($activeFeatures -join ', ')" -ForegroundColor Cyan
} else {
    Write-Host "  Features   :  none (raw extraction only)" -ForegroundColor DarkGray
}

if ($Guid) {
    Write-Host ""
    Write-Host "  >> Single-asset mode: only GUID $Guid will be processed." -ForegroundColor Yellow
}

# ============================================================
#  FULL COMMAND PREVIEW
# ============================================================
Write-Host ""
Write-Host "  Command:" -ForegroundColor DarkGray
$allArgs = @("`"$GamePath`"", "`"$OutputPath`"", $Language) + $extraFlags
Write-Host "  dotnet run --project `"$AssetProcessorProject`" -- $($allArgs -join ' ')" -ForegroundColor DarkGray
Write-Host ""
Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# ============================================================
#  RUN  (capture output for summary while streaming to console)
# ============================================================
$capturedLines  = [System.Collections.Generic.List[string]]::new()
$logPath        = Join-Path $OutputPath "debug-log.txt"
$runTimestamp   = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$exitCode       = 0

# Write log header before the run starts
$headerFlags = $extraFlags -join ' '
$logHeader = @()
$logHeader += "================================================================"
$logHeader += "  Asset Splitter -- Debug Log"
$logHeader += "  Run at  : $runTimestamp"
$logHeader += "  Game    : $GamePath"
$logHeader += "  Output  : $OutputPath"
$logHeader += "  Flags   : $headerFlags"
$logHeader += "================================================================"
$logHeader += ""
$logHeader | Set-Content -Path $logPath -Encoding UTF8

Push-Location $ProjectRoot
try {
    & dotnet run --project $AssetProcessorProject -- `
        "$GamePath" "$OutputPath" $Language @extraFlags 2>&1 |
    ForEach-Object {
        $line = "$_"
        Write-Host $line
        [void]$capturedLines.Add($line)
    }
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

# Flush full console output to log file
$capturedLines | Add-Content -Path $logPath -Encoding UTF8

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "  [ERROR] AssetProcessor exited with code $exitCode" -ForegroundColor Red
}

# ============================================================
#  PARSE CAPTURED OUTPUT
# ============================================================

# Total Phase 2 assets: take the MAX of all "Formatting" lines so we get the main run count.
# Lines may be [95,546/95,546] (with commas) or [556/556] (no commas). Parse both.
$formattingLines = $capturedLines | Where-Object { $_ -match '\[\s*[\d,]+\s*/\s*([\d,]+)\]\s*-\s*Formatting' }
$totalAssets = 0
foreach ($fl in $formattingLines) {
    if ($fl -match '\[\s*[\d,]+\s*/\s*([\d,]+)\]\s*-\s*Formatting') {
        $n = [int]($Matches[1] -replace ',', '')
        if ($n -gt $totalAssets) { $totalAssets = $n }
    }
}

# Locate the boundary between Phase 1 and Phase 2:
# Phase 2 starts after the FIRST "[COMPLETE] Final processing completed:" (asset splitter output).
# Using first occurrence ensures we include both main format pass and BaseAssetGUID pass in Phase 2.
$phase1CompleteIdx = -1
for ($i = 0; $i -lt $capturedLines.Count; $i++) {
    if ($capturedLines[$i] -match '^\[COMPLETE\] Final processing completed:') {
        $phase1CompleteIdx = $i
        break
    }
}

# Collect Phase 2 lines into a plain array.
$startIdx = if ($phase1CompleteIdx -ge 0) { $phase1CompleteIdx + 1 } else { 0 }
$phase2List = [System.Collections.ArrayList]::new()
$capturedCount = $capturedLines.Count
for ($i = $startIdx; $i -lt $capturedCount; $i++) {
    $line = $capturedLines.Item($i)
    [void]$phase2List.Add($line)
}
$phase2Lines = $phase2List.ToArray()

# "No Template node found" - only meaningful in Phase 2. Match "in 100563 - [ Name ].xml".
$noTemplateRegex = 'No Template node found in (\d+) - \[ (.+?) \]\.xml'
$noTemplateList = [System.Collections.ArrayList]::new()
foreach ($pl in $phase2Lines) {
    if ($pl -match $noTemplateRegex) {
        $ob = [PSCustomObject]@{
            GUID = $Matches[1]
            Name = $Matches[2].Trim()
        }
        $noTemplateList.Add($ob) | Out-Null
    }
}
$noTemplateList = $noTemplateList.ToArray()
# If no-Template count equals one of the Formatting pass counts (e.g. BaseAssetGUID pass = 556), treat as BaseAssetGUID inherited.
$formatRegex = '\[\s*[\d,]+\s*/\s*([\d,]+)\]\s*-\s*Formatting'
$formatPassCounts = $formattingLines | ForEach-Object {
    if ($_ -match $formatRegex) { [int]($Matches[1] -replace ',', '') } else { $null }
} | Where-Object { $null -ne $_ }
$allUseBaseAssetGUID = ($noTemplateList.Count -gt 0) -and ($formatPassCounts -contains $noTemplateList.Count)

# Warnings and errors from the backend (full run, both phases).
$warnLines  = $capturedLines | Where-Object { $_ -match '^\[WARN\]' }
$errorLines = $capturedLines | Where-Object { $_ -match '^\[ERROR\]' }
$warnCount  = $warnLines.Count   # used in Run stats and summary
$errorCount = $errorLines.Count  # used in Run stats and summary

# VectorElement removal stats - Phase 2 only to avoid inflated Phase 1 counts.
$vectorTotal = 0
$vectorFiles = 0
$phase2Lines | Where-Object { $_ -match 'Removed (\d+) VectorElement' } | ForEach-Object {
    $vectorTotal += [int]([regex]::Match($_, 'Removed (\d+) VectorElement').Groups[1].Value)
    $vectorFiles++
}

# ============================================================
#  DEBUG SUMMARY
# ============================================================
Write-Host ""
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "   DEBUG SUMMARY" -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host ""

# --- Run stats ---
Write-Host "  Run stats:" -ForegroundColor Cyan
if ($totalAssets -gt 0) {
    Write-Host "    Assets processed       :  $totalAssets" -ForegroundColor White
}
if ($vectorFiles -gt 0) {
    Write-Host "    VectorElement removed  :  $vectorTotal nodes across $vectorFiles files" -ForegroundColor White
} else {
    Write-Host "    VectorElement removed  :  0" -ForegroundColor DarkGray
}
if ($allUseBaseAssetGUID) {
    $ntCount = $noTemplateList.Count
    $msg1 = '    BaseAssetGUID assets   :  {0} / {1} assets use BaseAssetGUID inheritance  (no Template node, placed in BaseAssetGUID/)'
    Write-Host ($msg1 -f $ntCount, $totalAssets) -ForegroundColor Cyan
}
if (-not $allUseBaseAssetGUID -and $noTemplateList.Count -gt 0) {
    $ntCount = $noTemplateList.Count
    $msg2 = '    No Template node       :  {0} assets  (no Template or BaseAssetGUID, stayed in root output folder)'
    Write-Host ($msg2 -f $ntCount) -ForegroundColor Yellow
}
if (-not $allUseBaseAssetGUID -and $noTemplateList.Count -eq 0) {
    Write-Host "    No Template node       :  0" -ForegroundColor DarkGray
}
$warnColor = if ($warnLines.Count -gt 0) { "Yellow" } else { "DarkGray" }
$errColor = if ($errorLines.Count -gt 0) { "Red" } else { "DarkGray" }
Write-Host "    Warnings               :  $warnCount" -ForegroundColor $warnColor
Write-Host "    Errors                 :  $errorCount" -ForegroundColor $errColor

# --- BaseAssetGUID / no-template list ---
if ($allUseBaseAssetGUID) {
    Write-Host ""
    Write-Host "  BaseAssetGUID assets  ($($noTemplateList.Count)):" -ForegroundColor Cyan
    Write-Host "  These assets use BaseAssetGUID inheritance (no Template element)." -ForegroundColor DarkGray
    Write-Host "  They are correctly placed in:  output_xml_anno117/BaseAssetGUID/" -ForegroundColor DarkGray
    Write-Host ""
    $noTemplateList | ForEach-Object {
        Write-Host ("    {0,-10}  {1}" -f $_.GUID, $_.Name) -ForegroundColor Cyan
    }
} elseif ($noTemplateList.Count -gt 0) {
    Write-Host ""
    Write-Host "  No Template node  ($($noTemplateList.Count) assets):" -ForegroundColor Yellow
    Write-Host "  These assets have no Template or BaseAssetGUID -- saved in root output folder." -ForegroundColor DarkGray
    Write-Host ""
    $noTemplateList | ForEach-Object {
        Write-Host ("    {0,-10}  {1}" -f $_.GUID, $_.Name) -ForegroundColor Yellow
    }
}

# --- Warnings ---
if ($warnLines.Count -gt 0) {
    Write-Host ""
    Write-Host "  Warnings  ($($warnLines.Count)):" -ForegroundColor Yellow
    $warnLines | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
}

# --- Errors ---
if ($errorLines.Count -gt 0) {
    Write-Host ""
    Write-Host "  Errors  ($($errorLines.Count)):" -ForegroundColor Red
    $errorLines | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
}

if ($warnLines.Count -eq 0 -and $errorLines.Count -eq 0) {
    Write-Host ""
    Write-Host "  No warnings or errors." -ForegroundColor Green
}

# --- Save summary to file ---
$summaryPath    = Join-Path $OutputPath "debug-summary.txt"
$noTemplateLabel = if ($allUseBaseAssetGUID) { "BaseAssetGUID inherited (placed in BaseAssetGUID/)" } else { "No Template node (root folder)" }
$runTime = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
$flagsStr = $extraFlags -join ' '
$noTemplateCount = $noTemplateList.Count
$summaryLines = @()
$summaryLines += "Debug Summary"
$summaryLines += "Run at: $runTime"
$summaryLines += "Game:   $GamePath"
$summaryLines += "Flags:  $flagsStr"
$summaryLines += ""
$summaryLines += "Run stats:"
$summaryLines += "  Assets processed      : $totalAssets"
$summaryLines += "  VectorElement removed : $vectorTotal nodes across $vectorFiles files"
$summaryLines += "  $noTemplateLabel : $noTemplateCount assets"
$summaryLines += "  Warnings              : $warnCount"
$summaryLines += "  Errors                : $errorCount"
if ($noTemplateList.Count -gt 0) {
    $summaryLines += ""
    $summaryLines += "$noTemplateLabel ($noTemplateCount assets):"
    $noTemplateList | ForEach-Object { $g = $_.GUID; $n = $_.Name; $summaryLines += "  $g  $n" }
}
if ($warnLines.Count -gt 0) {
    $summaryLines += ""
    $summaryLines += "Warnings:"
    $warnLines | ForEach-Object { $summaryLines += "  $_" }
}
if ($errorLines.Count -gt 0) {
    $summaryLines += ""
    $summaryLines += "Errors:"
    $errorLines | ForEach-Object { $summaryLines += "  $_" }
}
$summaryLines | Set-Content -Path $summaryPath -Encoding UTF8

# ============================================================
#  DONE
# ============================================================
Write-Host ""
Write-Host "------------------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Debug run complete." -ForegroundColor Green
Write-Host "  Output  :  $OutputPath" -ForegroundColor Cyan
Write-Host "  Log     :  $logPath" -ForegroundColor Cyan
Write-Host "  Summary :  $summaryPath" -ForegroundColor Cyan
Write-Host ""

if ($exitCode -ne 0) {
    exit $exitCode
}
