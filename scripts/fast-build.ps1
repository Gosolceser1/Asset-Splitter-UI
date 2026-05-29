# Fast incremental build script - optimized for development speed
# Usage: .\fast-build.ps1 [-Clean] [-Configuration Debug|Release|Both] [-Verbose]

param(
    [switch]$Clean,
    [ValidateSet('Debug', 'Release', 'Both')]
    [string]$Configuration = 'Debug',
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "build-common.ps1")

$solutionPath = Get-SolutionPath
Assert-SolutionExists $solutionPath

function Invoke-FastBuild {
    param([string]$Config, [bool]$ShowVerbose)

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $verbosity = if ($ShowVerbose) { "normal" } else { "quiet" }

    $output = dotnet build $solutionPath -c $Config --no-restore -v $verbosity -m 2>&1
    $exitCode = $LASTEXITCODE
    $stopwatch.Stop()

    $warnings = ($output | Select-String -Pattern "(?i)warning [A-Z]{2}\d+" -AllMatches).Matches.Count
    $errors = ($output | Select-String -Pattern "(?i)error [A-Z]{2}\d+" -AllMatches).Matches.Count

    if ($ShowVerbose) {
        $output | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }

    if ($exitCode -eq 0) {
        $statusColor = if ($warnings -gt 0) { "Yellow" } else { "Green" }
        $timeStr = [math]::Round($stopwatch.Elapsed.TotalSeconds, 2)
        Write-Host "  [OK] Built in $timeStr`s" -ForegroundColor $statusColor -NoNewline
        if ($warnings -gt 0) { Write-Host " ( $warnings warnings )" -ForegroundColor Yellow } else { Write-Host "" }
        return $true
    }

    Write-Host "  [FAIL] Build FAILED ( $errors errors )" -ForegroundColor Red
    $errorLines = $output | Where-Object { $_ -match '(?i)\berror\b' }
    $errorLines | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    return $false
}

# MAIN
$totalStart = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host ""
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "           Fast Build - Development Mode" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan

if ($Clean) {
    Write-Step "Cleaning solution" "Cyan"
    dotnet clean $solutionPath -v quiet 2>&1 | Out-Null
    Write-Host "  [OK] Cleaned" -ForegroundColor DarkGreen
}

$buildSuccess = $true
$configs = if ($Configuration -eq 'Both') { @('Debug', 'Release') } else { @($Configuration) }

foreach ($config in $configs) {
    Write-Step "Building $config configuration" "Cyan"
    if (-not (Invoke-FastBuild -Config $config -ShowVerbose $Verbose)) {
        $buildSuccess = $false
        break
    }
}

$totalStart.Stop()
$totalTime = [math]::Round($totalStart.Elapsed.TotalSeconds, 2)

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor DarkGray

if ($buildSuccess) {
    Write-Host "  [OK] Build completed successfully in $totalTime`s" -ForegroundColor Green
    Write-Host ""
    exit 0
} else {
    Write-Host "  [FAIL] Build failed" -ForegroundColor Red
    Write-Host ""
    exit 1
}
