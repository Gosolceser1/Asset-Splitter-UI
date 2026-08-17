<#
.SYNOPSIS
    Asset Splitter UI -- single build, publish, and release entry point.
.DESCRIPTION
    One command for all build tasks:
      build.ps1                  Full clean build + publish zips
      build.ps1 -Fast            Quick incremental Debug build
      build.ps1 -CreateRelease   Build + publish + push + GitHub release
#>

param(
    [switch]$Fast,
    [switch]$CreateRelease,
    [switch]$SkipPublish,
    [switch]$ClearNuGetCache,
    [switch]$Verbose,
    [switch]$DebugOnly,
    [switch]$ReleaseOnly
)

$ErrorActionPreference = "Stop"

if ($DebugOnly -and $ReleaseOnly) {
    Write-Host "[FATAL] Cannot use -DebugOnly and -ReleaseOnly together." -ForegroundColor Red
    exit 1
}

$root = if ($PSScriptRoot) { Split-Path -Path $PSScriptRoot -Parent } else { (Get-Location).Path }

if ($CreateRelease) {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        $env:Path += ";C:\Program Files\GitHub CLI"
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
            Write-Host "[FATAL] GitHub CLI (gh) not found. Install: winget install --id GitHub.cli" -ForegroundColor Red
            exit 1
        }
    }
    & gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FATAL] Not logged into GitHub. Run: gh auth login" -ForegroundColor Red
        exit 1
    }
}

if ($Fast) {
    $config = if ($ReleaseOnly) { "Release" } else { "Debug" }
    Write-Host "Fast build: $config" -ForegroundColor Cyan
    & powershell -ExecutionPolicy Bypass -File "$root\scripts\fast-build.ps1" -Configuration $config
    $code = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } elseif (-not $?) { 1 } else { 0 }
    if ($code -ne 0) { Write-Host "[FAIL] Fast build exited $code" -ForegroundColor Red }
    exit $code
}

$passArgs = @()
if ($DebugOnly)      { $passArgs += "-DebugOnly" }
if ($ReleaseOnly)    { $passArgs += "-ReleaseOnly" }
if ($SkipPublish)    { $passArgs += "-SkipPublish" }
if ($ClearNuGetCache) { $passArgs += "-ClearNuGetCache" }
if ($Verbose)        { $passArgs += "-Verbose" }
if ($CreateRelease)  { $passArgs += "-CreateRelease" }

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host "          Asset Splitter UI -- Build Pipeline" -ForegroundColor Magenta
Write-Host "======================================================================" -ForegroundColor Magenta

& powershell -ExecutionPolicy Bypass -File "$root\scripts\clean-build.ps1" @passArgs
$code = if ($null -ne $LASTEXITCODE) { $LASTEXITCODE } elseif (-not $?) { 1 } else { 0 }
if ($code -ne 0) { Write-Host ""; Write-Host "[FAIL] Pipeline exited $code" -ForegroundColor Red }
exit $code
