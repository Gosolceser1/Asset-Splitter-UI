# Shared helpers for build scripts (clean-build.ps1, fast-build.ps1, debug-run.ps1)
# Source with: . (Join-Path $PSScriptRoot "build-common.ps1")

function Write-StepHeader {
    param([string]$Message, [string]$Color = "Yellow")
    Write-Host ""
    Write-Host "======================================================================" -ForegroundColor DarkGray
    Write-Host "  $Message" -ForegroundColor $Color
    Write-Host "======================================================================" -ForegroundColor DarkGray
}

function Write-Status {
    param([string]$Status, [string]$Message, [string]$Color = "Gray")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "  [$timestamp] [$Status]" -ForegroundColor $Color -NoNewline
    Write-Host " $Message" -ForegroundColor Gray
}

function Write-Elapsed {
    param([double]$Seconds)
    $mins = [math]::Floor($Seconds / 60)
    $secs = [math]::Round($Seconds % 60, 1)
    if ($mins -gt 0) { "$($mins)m $($secs)s" } else { "$($secs)s" }
}

function Write-FileCount {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path $Path)) { Write-Host "  $Label`: not found" -ForegroundColor DarkGray; return }
    $fileCount = (Get-ChildItem $Path -File -Recurse -ErrorAction SilentlyContinue | Measure-Object).Count
    $dirCount = (Get-ChildItem $Path -Directory -Recurse -ErrorAction SilentlyContinue | Measure-Object).Count
    $sizeBytes = (Get-ChildItem $Path -File -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $sizeMB = [math]::Round($sizeBytes / 1MB, 2)
    Write-Host "  $Label`: $fileCount files in $dirCount folders ($sizeMB MB)" -ForegroundColor DarkGray
}

function Get-SolutionPath {
    $solutionPath = Join-Path (Join-Path $PSScriptRoot "..") "AssetSplitter.sln"
    return (Resolve-Path $solutionPath -ErrorAction SilentlyContinue).Path
}

function Get-ProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-AssetProcessorProject {
    return Join-Path (Get-ProjectRoot) "src\AssetSplitter\AssetSplitter.csproj"
}

function Get-UIProject {
    return Join-Path (Get-ProjectRoot) "src\AssetSplitterUI\AssetSplitterUI.csproj"
}

function Get-VersionFromCsproj {
    param([string]$UiProj)
    $content = Get-Content $UiProj -Raw
    if ($content -match '<Version>([^<]+)</Version>') { return $Matches[1].Trim() }
    return "1.0.0"
}

function Write-Step {
    param([string]$Message, [string]$Color = "Yellow")
    Write-Host ""
    Write-Host "-------------------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  $Message" -ForegroundColor $Color
    Write-Host "-------------------------------------------------------------------" -ForegroundColor DarkGray
}

function Invoke-DotnetBuild {
    param(
        [string]$Configuration,
        [string]$SolutionPath,
        [bool]$ShowVerbose,
        [bool]$NoRestore,
        [ref]$OutWarnings,
        [ref]$OutErrors
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host "  Building $Configuration..." -ForegroundColor Cyan

    $output = if ($NoRestore) {
        dotnet build $SolutionPath -c $Configuration --no-restore 2>&1
    } else {
        dotnet build $SolutionPath -c $Configuration 2>&1
    }
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()
    $elapsed = $stopwatch.Elapsed.TotalSeconds

    $warns = ($output | Select-String -Pattern "(?i)warning [A-Z]{2}\d+" -AllMatches).Matches.Count
    $errsWithCodes = ($output | Select-String -Pattern "(?i)error [A-Z]{2}\d+" -AllMatches).Matches.Count
    $genericErrs = ($output | Where-Object { $_ -match '(?i)\berror\b' -and $_ -notmatch '(?i)warning' }).Count
    $errs = [math]::Max($errsWithCodes, $genericErrs)
    $OutWarnings.Value += $warns
    $OutErrors.Value += $errs

    if ($ShowVerbose) {
        $output | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }

    if ($exitCode -eq 0) {
        $statusColor = if ($warns -gt 0) { "Yellow" } else { "Green" }
        $elapsedStr = [math]::Round($elapsed, 1)
        Write-Host "  [OK] $Configuration build succeeded in $elapsedStr`s" -ForegroundColor $statusColor
        if ($warns -gt 0) { Write-Host "    ( $warns warning(s) )" -ForegroundColor Yellow }
        return $true
    }

    if ($errs -lt 1) { $errs = 1 }
    Write-Host "  [FAIL] $Configuration build FAILED ( $errs error(s) )" -ForegroundColor Red
    $errorLines = $output | Where-Object { $_ -match '(?i)\berror\b' -and $_ -notmatch '(?i)warning' }
    if ($errorLines) {
        $errorLines | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    }
    if (-not $ShowVerbose) {
        Write-Host "    --- build output (last 40 lines) ---" -ForegroundColor DarkGray
        $output | Select-Object -Last 40 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }
    return $false
}

function Assert-SolutionExists($SolutionPath) {
    if (-not (Test-Path $SolutionPath)) {
        Write-Host "[ERROR] Solution not found: $SolutionPath" -ForegroundColor Red
        exit 1
    }
}
