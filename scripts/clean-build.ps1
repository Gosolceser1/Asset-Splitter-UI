# Full clean-build script for the AnnoAssetSplitterUI solution.
#
# What it does end-to-end:
#   Pre  - Shuts down dotnet build servers so no DLLs are locked during cleanup.
#   1    - Kills any running AssetSplitter / AssetProcessor / RDAExtract processes (same reason).
#   2    - Deletes all bin/, obj/, lib/, and .vs/ folders under the repo root.
#   3    - Clears the local NuGet package cache (optional, off by default; use -ClearNuGetCache).
#   3b   - Checks for outdated NuGet packages and optionally updates them.
#   4    - Runs 'dotnet clean' on the solution, then restores all NuGet packages once.
#   5    - Builds Debug and/or Release (both by default, restore skipped because step 4 did it).
#   6    - Publishes the UI project for win-x64 in two flavors (self-contained and
#          framework-dependent), copies config/ and Localization/ into each publish
#          folder, and zips both into artifacts/releases/ for distribution.
#
# Usage:
#   .\clean-build.ps1                    # Full clean build + publish (NuGet cache NOT cleared)
#   .\clean-build.ps1 -ClearNuGetCache    # Also clear global NuGet cache (needed if packages are corrupt)
#   .\clean-build.ps1 -DebugOnly        # Only build Debug; skip Release and publish
#   .\clean-build.ps1 -ReleaseOnly      # Only build Release
#   .\clean-build.ps1 -SkipPublish      # Build but don't publish or zip
#   .\clean-build.ps1 -CreateRelease    # Also upload zips to GitHub release (requires gh CLI)
#   .\clean-build.ps1 -Verbose          # Print full dotnet output
#   .\clean-build.ps1 -UpdatePackages   # Show outdated packages and ask before updating
#   .\clean-build.ps1 -AutoUpdate       # Same as default (auto-update all packages)

param(
    # Force-clear the global NuGet package cache before restoring. Off by default because
    # the IDE holds analyzer DLLs open, causing NuGet to fail when re-extracting packages.
    [switch]$ClearNuGetCache,

    # Only build the Debug configuration. Skips Release and the publish/zip step entirely.
    [switch]$DebugOnly,

    # Only build the Release configuration. Skips Debug.
    [switch]$ReleaseOnly,

    # Skip Step 6 (publish win-x64 + zip). Useful when you only need compiled binaries.
    [switch]$SkipPublish,

    # Print full dotnet build/restore/publish output instead of the condensed summary.
    [switch]$Verbose,

    # Query NuGet for outdated packages, display a diff, and prompt before updating.
    [switch]$UpdatePackages,

    # Same as -UpdatePackages but applies all updates automatically without prompting.
    [switch]$AutoUpdate,

    # Upload the release zips to GitHub as a new release (step 7).
    [switch]$CreateRelease
)

# Fail immediately on any error so problems surface early rather than cascading.
$ErrorActionPreference = "Stop"

# Source shared helpers
. (Join-Path $PSScriptRoot "build-common.ps1")

# Accumulated across all build steps for the final summary line.
$script:totalWarnings = 0
$script:totalErrors = 0

$solutionPath = Get-SolutionPath
$rootPath = Get-ProjectRoot

Assert-SolutionExists $solutionPath

# Safe native command runner: captures stderr without crashing on $ErrorActionPreference="Stop"
function Invoke-NativeCommand {
    param([scriptblock]$Command)
    $old = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $Command 2>&1 | Out-Null
    $ErrorActionPreference = $old
}

# ---------------------------------------------------------------------------
# Helper: kills any AssetSplitter / AssetProcessor / RDAExtract processes that are running.
# These can hold locks on output DLLs, causing the subsequent file-delete step
# to fail silently or leave stale binaries behind.
# Returns $true if any processes were stopped, $false otherwise.
# ---------------------------------------------------------------------------
function Stop-AnnoProcesses {
    $processes = Get-Process | Where-Object { 
        $_.ProcessName -like "*AssetSplit*" -or 
        $_.ProcessName -like "*AnnoAssetSplitter*" -or
        $_.ProcessName -like "*AssetProcessor*" -or
        $_.ProcessName -like "*RDAExtract*"
    }
    
    if ($processes) {
        Write-Host "  Stopping $($processes.Count) running process(es)..." -ForegroundColor Gray
        $processes | ForEach-Object {
            Write-Host "    - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor DarkGray
            $_ | Stop-Process -Force -ErrorAction SilentlyContinue
        }
        # Brief pause so the OS releases file handles before the delete step runs.
        Start-Sleep -Milliseconds 500
        return $true
    }
    Write-Host "  No Anno processes running" -ForegroundColor DarkGreen
    return $false
}

# ---------------------------------------------------------------------------
# Helper: deletes all bin/, obj/, lib/, and .vs/ folders under the repo root.
# Uses a single recursive directory scan (instead of four separate passes) so
# large trees are traversed only once. Reports total folder count and MB freed.
# ---------------------------------------------------------------------------
function Remove-BuildArtifacts {
    $folders = @('bin', 'obj', 'lib', '.vs')
    # HashSet with case-insensitive comparison so folder names match on Windows
    # regardless of casing (e.g. "BIN" == "bin").
    $folderSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($folder in $folders) { [void]$folderSet.Add($folder) }

    $removedCount = 0
    $totalSize = 0

    # Single tree scan to avoid 4x full-directory traversal
    $items = Get-ChildItem -Path $rootPath -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $folderSet.Contains($_.Name) } |
        Sort-Object -Property FullName -Unique

    foreach ($item in $items) {
        try {
            $size = (Get-ChildItem $item.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
            $totalSize += $size
            Remove-Item $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
            $removedCount++
        } catch {
            # Ignore errors for locked files
        }
    }

    $sizeMB = [math]::Round($totalSize / 1MB, 2)
    Write-Host "  Removed $removedCount folder(s), freed ~$sizeMB MB" -ForegroundColor DarkGreen
}

# ---------------------------------------------------------------------------
# Helper: publishes the AssetSplitterUI project for win-x64 in either
# self-contained mode (ships its own .NET runtime, no install required) or
# framework-dependent mode (smaller download, requires .NET 10 on the user's
# machine). After publishing, refreshes config/ and Localization/ in the output
# folder, then zips the whole publish directory into artifacts/releases/ ready
# for a GitHub Release.
# ---------------------------------------------------------------------------
function Invoke-PublishStep {
    param(
        [string]$RootPath,
        [bool]$ShowVerbose,
        # $true = self-contained (includes .NET runtime); $false = framework-dependent.
        [bool]$SelfContained = $true
    )
    $uiProj = Join-Path $RootPath "src/AssetSplitterUI/AssetSplitterUI.csproj"
    $subDir = if ($SelfContained) { "win-x64" } else { "win-x64-framework" }
    $publishDir = Join-Path $RootPath "artifacts/publish/$subDir"
    if (-not (Test-Path $uiProj)) {
        Write-Host "  [FAIL] Project not found: $uiProj" -ForegroundColor Red
        return $false
    }
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }
    $verb = if ($ShowVerbose) { "normal" } else { "minimal" }
    $sc = if ($SelfContained) { "true" } else { "false" }
    $label = if ($SelfContained) { "self-contained" } else { "framework-dependent (.NET 10 required)" }
    Write-Host "  Publishing $label..." -ForegroundColor Gray

    # If NUGET_PACKAGES is still pointing at the temp build cache (set by Invoke-Restore
    # fallback), keep it so publish uses the same already-extracted packages.
    # Otherwise redirect to the same temp path to avoid the IDE lock on the global cache.
    $savedNugetPackages = $env:NUGET_PACKAGES
    if (-not $env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES = Join-Path $env:TEMP "NuGetBuild"
    }

    $pubOut = dotnet publish $uiProj -c Release -f net10.0 -r win-x64 --self-contained $sc -o $publishDir -v $verb 2>&1
    $pubExit = $LASTEXITCODE
    $env:NUGET_PACKAGES = $savedNugetPackages

    if ($pubExit -ne 0) {
        Write-Host "  [FAIL] Publish failed" -ForegroundColor Red
        $pubOut | Where-Object { $_ -match "error" } | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        return $false
    }

    # Refresh runtime data folders in the publish output so release zips never
    # carry stale config or localization files from a previous refactor.
    $configSrc = Join-Path $RootPath "config"
    $locSrc = Join-Path $RootPath "Localization"
    $configDest = Join-Path $publishDir "config"
    $locDest = Join-Path $publishDir "Localization"
    if (Test-Path $configSrc) {
        if (Test-Path $configDest) { Remove-Item -LiteralPath $configDest -Recurse -Force }
        Copy-Item -Path $configSrc -Destination $configDest -Recurse -Force
        Write-FileCount $configDest "  config/"
    }
    if (Test-Path $locSrc) {
        if (Test-Path $locDest) { Remove-Item -LiteralPath $locDest -Recurse -Force }
        Copy-Item -Path $locSrc -Destination $locDest -Recurse -Force
        Write-FileCount $locDest "  Localization/"
    }

    # Read the version from the csproj so the zip filename always matches the release.
    $version = "1.0.0"
    $csprojContent = Get-Content $uiProj -Raw
    if ($csprojContent -match '<Version>([^<]+)</Version>') { $version = $Matches[1].Trim() }
    $zipSuffix = if ($SelfContained) { "win-x64-self-contained" } else { "win-x64-framework" }
    $zipName = "Asset-Splitter-UI-v$version-$zipSuffix.zip"
    $releaseDir = Join-Path $RootPath "artifacts/releases"
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
    $zipPath = Join-Path $releaseDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $publishDir -DestinationPath $zipPath -CompressionLevel Optimal
    $zipSize = (Get-Item $zipPath).Length
    $zipSizeMB = [math]::Round($zipSize / 1MB, 1)
    Write-Host "  [OK] $zipName ($zipSizeMB MB)" -ForegroundColor Green
    return $true
}

# ---------------------------------------------------------------------------
# Helper: checks for outdated NuGet packages across the solution.
# Parses 'dotnet list package --outdated' output into structured objects,
# then either prompts the user or auto-applies updates depending on $Auto.
# Updates are applied per-project via 'dotnet add package'.
# ---------------------------------------------------------------------------
function Invoke-PackageUpdate {
    param(
        [string]$SolutionPath,
        [bool]$Auto
    )

    Write-Host "  Checking NuGet packages..." -ForegroundColor Cyan

    # dotnet list package internally runs a restore, which hits the same IDE file-lock
    # on the global NuGet cache. Redirect to the temp cache (same one used by Invoke-Restore)
    # so the query succeeds regardless of IDE state.
    $savedPkg = $env:NUGET_PACKAGES
    if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $env:TEMP "NuGetBuild" }

    # Get ALL packages (current versions)
    $allRaw = dotnet list $SolutionPath package 2>&1
    $allExit = $LASTEXITCODE

    if ($allExit -ne 0) {
        $env:NUGET_PACKAGES = $savedPkg
        $isLock = $allRaw | Where-Object { $_ -match "Access to the path" -and $_ -match "is denied" }
        if ($isLock) {
            Write-Host "  [WARN] IDE lock prevented package query. Skipping update check." -ForegroundColor Yellow
        } else {
            Write-Host "  [WARN] Could not query NuGet (offline?). Skipping." -ForegroundColor Yellow
        }
        return
    }

    # Get OUTDATED packages (latest versions)
    $outRaw = dotnet list $SolutionPath package --outdated 2>&1
    $env:NUGET_PACKAGES = $savedPkg

    # Build a list of all packages with current + latest versions
    $allPackages = @{}
    $currentProject = ""
    foreach ($line in $allRaw) {
        if ($line -match "^Project\s+`"([^`"]+)`"") { $currentProject = $Matches[1] }
        if ($line -match "^\s+>\s+(\S+)\s+(\S+)") {
            $allPackages[$Matches[1]] = @{ Project = $currentProject; Current = $Matches[2]; Latest = $Matches[2] }
        }
    }

    # Parse outdated to get latest versions
    $updates = @()
    $currentProject = ""
    foreach ($line in $outRaw) {
        if ($line -match "^Project\s+`"([^`"]+)`"") { $currentProject = $Matches[1] }
        if ($line -match "^\s+>\s+(\S+)\s+(\S+)\s+(\S+)\s+(\S+)") {
            $pkg = $Matches[1]
            $latest = $Matches[4]
            if ($allPackages.ContainsKey($pkg)) {
                $allPackages[$pkg].Latest = $latest
            }
            $updates += [pscustomobject]@{ Package = $pkg; Current = $Matches[2]; Latest = $latest; Project = $currentProject }
        }
    }

    # Display ALL packages table
    Write-Host ""
    Write-Host ("  {0,-35} {1,-14} {2,-14} {3}" -f "Package", "Current", "Latest", "Status") -ForegroundColor White
    Write-Host ("  {0,-35} {1,-14} {2,-14} {3}" -f "---", "---", "---", "---") -ForegroundColor DarkGray
    foreach ($pkg in $allPackages.Keys | Sort-Object) {
        $info = $allPackages[$pkg]
        $current = $info.Current
        $latest = $info.Latest
        $outdated = $current -ne $latest
        $status = if ($outdated) { "UPDATE" } else { "OK" }
        $color = if ($outdated) { "Yellow" } else { "DarkGreen" }
        Write-Host ("  {0,-35} {1,-14} {2,-14} {3}" -f $pkg, $current, $latest, $status) -ForegroundColor $color
    }
    Write-Host ""

    if (-not $updates -or $updates.Count -eq 0) {
        Write-Host "  [OK] All $($allPackages.Count) packages are up-to-date" -ForegroundColor DarkGreen
        return
    }

    Write-Host "  $($updates.Count) package(s) can be updated" -ForegroundColor Yellow

    $doUpdate = $Auto
    if (-not $Auto) {
        $answer = Read-Host "  Update all packages now? [y/N]"
        $doUpdate = $answer -match '^[yY]'
    }

    if (-not $doUpdate) {
        Write-Host "  [Skipped] Package update skipped" -ForegroundColor DarkGray
        return
    }

    $projectFiles = dotnet sln $SolutionPath list 2>&1 |
        Where-Object { $_ -match "\.csproj$" } |
        ForEach-Object { Join-Path (Split-Path $SolutionPath) $_.Trim() }

    $updatedCount = 0
    foreach ($update in $updates) {
        $targetProj = $projectFiles | Where-Object {
            (Get-Content $_ -Raw) -match [regex]::Escape($update.Package)
        } | Select-Object -First 1

        if (-not $targetProj) {
            Write-Host "  [WARN] Could not locate project for $($update.Package)" -ForegroundColor Yellow
            continue
        }

        Write-Host ("  Updating {0} {1} -> {2}..." -f $update.Package, $update.Current, $update.Latest) -ForegroundColor Cyan
        $addSaved = $env:NUGET_PACKAGES
        if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $env:TEMP "NuGetBuild" }
        dotnet add $targetProj package $update.Package --version $update.Latest 2>&1 | Out-Null
        $env:NUGET_PACKAGES = $addSaved
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] $($update.Package) updated" -ForegroundColor DarkGreen
            $updatedCount++
        } else {
            Write-Host "  [FAIL] Could not update $($update.Package)" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  Updated $updatedCount / $($updates.Count) package(s)" -ForegroundColor $(if ($updatedCount -eq $updates.Count) { "Green" } else { "Yellow" })
}

# ---------------------------------------------------------------------------
# Helper: restores all NuGet packages for the solution.
#
# The documented root cause of "Access to path ... is denied" on restore is
# running 'dotnet nuget locals all --clear' while the IDE is open — the IDE
# holds read locks on analyzer DLLs inside the global package cache, so NuGet
# cannot re-extract them (Microsoft Learn / NuGet docs confirm this).
#
# Strategy used here:
#   1. Delete per-project obj/project.nuget.cache files so NuGet is forced to
#      re-evaluate without needing to write into the locked global cache.
#   2. Run restore with --force-evaluate so NuGet re-checks all packages but
#      skips re-extracting DLLs that are already present and unchanged.
#   3. If that still fails with a lock error, fall back to a packages/ folder
#      local to the repo (NUGET_PACKAGES env var) so we never touch the global
#      cache that the IDE has open.
# ---------------------------------------------------------------------------
function Invoke-Restore {
    param(
        [string]$SolutionPath,
        [bool]$ShowVerbose
    )

    Write-Host "  Restoring packages..." -ForegroundColor Cyan

    # Attempt 1: standard restore with --force-evaluate.
    # --force-evaluate ignores the "up to date" restore cache (obj/*.nuget.cache)
    # so it re-checks all packages without re-extracting already-present DLLs.
    $output = dotnet restore $SolutionPath --force-evaluate -v q 2>&1
    $exitCode = $LASTEXITCODE
    if ($ShowVerbose) { $output | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray } }

    if ($exitCode -eq 0) {
        Write-Host "  [OK] Restore completed" -ForegroundColor Green
        return $true
    }

    $isLockError = $output | Where-Object { $_ -match "Access to the path" -and $_ -match "is denied" }
    if ($isLockError) {
        $lockedFile = if (($isLockError | Select-Object -First 1) -match "'([^']+)'") { [System.IO.Path]::GetFileName($Matches[1]) } else { "unknown" }
        Write-Host "  [WARN] Lock on '$lockedFile' (IDE holding global cache). Falling back to temp NuGet cache..." -ForegroundColor Yellow

        # Attempt 2: redirect NUGET_PACKAGES to a private temp folder that the IDE
        # is not watching, so NuGet can extract packages freely without hitting the lock.
        # Use $env:TEMP\NuGetBuild so it persists across script invocations (avoids
        # re-downloading every run), but is completely separate from the IDE-locked global cache.
        $tempPkgDir = Join-Path $env:TEMP "NuGetBuild"
        $savedNugetPackages = $env:NUGET_PACKAGES
        $env:NUGET_PACKAGES = $tempPkgDir
        Write-Host "  [INFO] NUGET_PACKAGES -> $tempPkgDir" -ForegroundColor DarkGray

        $output2 = dotnet restore $SolutionPath --force-evaluate -v q 2>&1
        $exitCode2 = $LASTEXITCODE
        if ($ShowVerbose) { $output2 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray } }

        if ($exitCode2 -eq 0) {
            Write-Host "  [OK] Restore completed (temp NuGet cache used)" -ForegroundColor Green
            # Keep NUGET_PACKAGES pointing at the temp dir for the build + publish steps
            # so they also use the already-extracted packages. Caller must clear it after done.
            return $true
        }
        $env:NUGET_PACKAGES = $savedNugetPackages
        $output = $output2
    }

    Write-Host "  [FAIL] Restore FAILED" -ForegroundColor Red
    if ($output) {
        $output | Select-Object -Last 40 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    }
    return $false
}

# ============================================================================
# MAIN SCRIPT
# ============================================================================

$scriptStart = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host ""
Write-Host "======================================================================" -ForegroundColor Magenta
Write-Host "          AnnoAssetSplitterUI - Clean Build Script" -ForegroundColor Magenta
Write-Host "======================================================================" -ForegroundColor Magenta

# Pre-step: shut down the dotnet build server so no background compiler process
# holds open DLLs in bin/ that would cause silent delete failures in Step 2.
Write-Step "Pre-step: Shutting down dotnet build servers" "Cyan"
Write-Status "PRE" "Shutting down dotnet build servers..."
dotnet build-server shutdown 2>&1 | Out-Null
Write-Status "OK" "Build servers shut down" "Green"

# Step 1: kill any running instances of the app for the same reason.
Write-Step "Step 1: Stopping Anno processes"
$stopped = Stop-AnnoProcesses
if (-not $stopped) { Write-Status "OK" "No running Anno processes" "Green" }

# Step 2: delete bin/, obj/, lib/, .vs/ so the subsequent build starts from scratch.
Write-Step "Step 2: Removing build artifacts"
Write-Status "CLEAN" "Scanning for bin/, obj/, lib/, .vs/ folders..."
Remove-BuildArtifacts

# Step 3: clear the local NuGet cache so packages are re-downloaded fresh.
# Use -ClearNuGetCache to also wipe the global NuGet cache (off by default to avoid IDE lock conflicts).
if ($ClearNuGetCache) {
    Write-Step "Step 3: Clearing NuGet cache"
    Write-Status "CLEAR" "Clearing local NuGet cache..."
    dotnet nuget locals all --clear 2>&1 | Out-Null
    Write-Status "OK" "NuGet cache cleared" "Green"
} else {
    Write-Host ""
    Write-Status "SKIP" "NuGet cache clear skipped (use -ClearNuGetCache to enable)" "DarkGray"
}

# Step 3b: Check for and update outdated NuGet packages.
    #   No flags   -> auto-update outdated packages without prompting
    #   -UpdatePackages -> show diff and ask before updating
    #   -AutoUpdate     -> force update without prompting (same as default)
#   -ClearNuGetCache still applies if passed
Write-Step "Step 3b: NuGet package update check"
if ($UpdatePackages) {
    # -UpdatePackages: show diff and prompt before applying
    Invoke-PackageUpdate -SolutionPath $solutionPath -Auto $false
} elseif ($AutoUpdate) {
    # -AutoUpdate: apply all updates without prompting
    Invoke-PackageUpdate -SolutionPath $solutionPath -Auto $true
} else {
    # Default (no flags): auto-update all outdated packages
    Invoke-PackageUpdate -SolutionPath $solutionPath -Auto $true
}

# Step 4: 'dotnet clean' removes any MSBuild-tracked outputs that the manual
# folder delete might have missed, then a single restore downloads all packages
# so neither build step below needs to repeat it.
Write-Step "Step 4: Cleaning solution"
Write-Status "CLEAN" "Running dotnet clean..."
$cleanWatch = [System.Diagnostics.Stopwatch]::StartNew()
dotnet clean $solutionPath -v q 2>&1 | Out-Null
$cleanWatch.Stop()
Write-Status "OK" "Solution cleaned in $(Write-Elapsed $cleanWatch.Elapsed.TotalSeconds)" "Green"

Write-Step "Step 4b: Restoring packages"
Write-Status "RESTORE" "Restoring NuGet packages for all projects..."
$restoreWatch = [System.Diagnostics.Stopwatch]::StartNew()
if (-not (Invoke-Restore -SolutionPath $solutionPath -ShowVerbose $Verbose)) {
    Write-Host ""; Write-Host "  [FAIL] Build failed with 1 error(s)" -ForegroundColor Red; Write-Host ""; exit 1
}
$restoreWatch.Stop()
Write-Status "OK" "Restore completed in $(Write-Elapsed $restoreWatch.Elapsed.TotalSeconds)" "Green"

# Step 5: build Debug and/or Release.
# -NoRestore $true skips the per-build restore since step 4b already ran it.
# -DebugOnly / -ReleaseOnly skip the other configuration as requested.
$buildSuccess = $true

if (-not $ReleaseOnly) {
    Write-Step "Step 5a: Building Debug"
    if (-not (Invoke-DotnetBuild -Configuration "Debug" -SolutionPath $solutionPath -ShowVerbose $Verbose -NoRestore $true -OutWarnings ([ref]$script:totalWarnings) -OutErrors ([ref]$script:totalErrors))) {
        $buildSuccess = $false
    }
}

if (-not $DebugOnly) {
    $stepNum = if ($ReleaseOnly) { "5" } else { "5b" }
    Write-Step "Step $stepNum`: Building Release"
    if (-not (Invoke-DotnetBuild -Configuration "Release" -SolutionPath $solutionPath -ShowVerbose $Verbose -NoRestore $true -OutWarnings ([ref]$script:totalWarnings) -OutErrors ([ref]$script:totalErrors))) {
        $buildSuccess = $false
    }
}

# Step 6: publish win-x64 (self-contained and framework-dependent), copy runtime
# data folders, and produce release zips. Skipped if any build step failed,
# if -SkipPublish was passed, or if only Debug was built (-DebugOnly).
if ($buildSuccess -and (-not $SkipPublish) -and (-not $DebugOnly)) {
    Write-Step "Step 6: Publish (win-x64 self-contained and framework-dependent)"
    if (-not (Invoke-PublishStep -RootPath $rootPath -ShowVerbose $Verbose -SelfContained $true)) {
        $buildSuccess = $false
    }
    if ($buildSuccess -and (-not (Invoke-PublishStep -RootPath $rootPath -ShowVerbose $Verbose -SelfContained $false))) {
        $buildSuccess = $false
    }
}

# Step 7: squash-push + tag + GitHub release (only when -CreateRelease is passed).
if ($CreateRelease -and $buildSuccess -and (-not $DebugOnly)) {
    Write-Step "Step 7: Push + GitHub release"

    $uiProj = Get-UIProject
    $version = Get-VersionFromCsproj -UiProj $uiProj
    $releaseDir = Join-Path $rootPath "artifacts/releases"
    $scZip  = Join-Path $releaseDir "Asset-Splitter-UI-v$version-win-x64-self-contained.zip"
    $fwZip  = Join-Path $releaseDir "Asset-Splitter-UI-v$version-win-x64-framework.zip"

    # 7a - Squash, commit, tag, and push to GitHub via dedicated script.
    # Uses -Force to skip the interactive prompt (we are already in an automated pipeline).
    # The squash script handles: orphan branch, git add -A, commit, replace main, force-push, tag.
    Write-Host "  Squashing history and pushing to origin main..." -ForegroundColor Gray
    $pushScript = Join-Path $PSScriptRoot "squash-history-and-push.ps1"
    $commitMsg = "Asset Splitter UI v$version"
    & powershell -ExecutionPolicy Bypass -File $pushScript -Message $commitMsg -Force
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [FAIL] squash-history-and-push.ps1 failed (exit $LASTEXITCODE)" -ForegroundColor Red
        $buildSuccess = $false
    } else {
        Write-Host "  [OK] Pushed to origin main + tag $version" -ForegroundColor Green
    }

    # 7c - Upload zips and screenshots to GitHub release
    if (-not (Test-Path $scZip) -or -not (Test-Path $fwZip)) {
        Write-Host "  [SKIP] Release zips not found" -ForegroundColor Yellow
    } else {
        Write-Host "  Deleting old release assets..." -ForegroundColor Gray
        Invoke-NativeCommand { gh release delete-asset $version "Asset-Splitter-UI-v$version-win-x64-self-contained.zip" --repo Gosolceser1/Asset-Splitter-UI -y }
        Invoke-NativeCommand { gh release delete-asset $version "Asset-Splitter-UI-v$version-win-x64-framework.zip" --repo Gosolceser1/Asset-Splitter-UI -y }

        $releaseNotes = @"
Extract and split Anno 1800 & Anno 117 game assets into mod-ready XML files.

## Features
- **Two-game support** — Anno 1800 and Anno 117: Pax Romana (C# / .NET 10)
- **GUID name resolution** — translates numeric GUIDs into readable names via game text files
- **Template-based sorting** — organizes assets by template type into clean folder structures
- **Dependency fixing** — resolves BaseAssetGUID chains for complete inheritance trees
- **Asset mod packaging** — generates ready-to-use Mod Loader folders with localized READMEs
- **Single GUID mode** — extract and test one asset without touching full outputs
- **ModOp education** — generated README includes full ModOp type reference with examples
- **Regional ingredients** — automates region-specific ingredient XML generation
- **11 languages** — UI, console output, modinfo, and README fully localized (EN, DE, FR, ES, IT, PL, RU, JA, KO, ZH, TW)
- **Developer tools** — secret unlock with copy-report, asset browser, and verbose console

## Screenshots
![Dark theme](https://raw.githubusercontent.com/Gosolceser1/Asset-Splitter-UI/main/assets/screenshots/screenshot-dark.png)
![Light theme](https://raw.githubusercontent.com/Gosolceser1/Asset-Splitter-UI/main/assets/screenshots/screenshot-light.png)

## Downloads
- **Self-contained** — no .NET install required (72 MB)
- **Framework-dependent** — requires .NET 10 runtime (39 MB)
"@

        Write-Host "  Creating release..." -ForegroundColor Gray
        Invoke-NativeCommand { gh release delete $version --repo Gosolceser1/Asset-Splitter-UI --yes }
        $relResult = & gh release create $version $scZip $fwZip `
            --title "Asset Splitter UI v$version" `
            --notes $releaseNotes `
            --repo Gosolceser1/Asset-Splitter-UI 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  [OK] Release created" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] Release failed: $relResult" -ForegroundColor Red
        }
    }

    # 7d - Verify everything synced
    Write-Host ""
    Write-Host "  --- Sync verification ---" -ForegroundColor Cyan
    $ok = $true
    $localHead = git -C $rootPath rev-parse HEAD
    $remoteHead = git -C $rootPath ls-remote origin refs/heads/main 2>$null | ForEach-Object { ($_ -split '\s+')[0] }
    $tagHead = git -C $rootPath ls-remote origin refs/tags/$version 2>$null | ForEach-Object { ($_ -split '\s+')[0] }

    if ($localHead -eq $remoteHead) { Write-Host "  [PASS] main branch" -ForegroundColor Green }
    else { Write-Host "  [FAIL] main branch  local=$($localHead.Substring(0,7)) remote=$($remoteHead.Substring(0,7))" -ForegroundColor Red; $ok = $false }

    if ($localHead -eq $tagHead) { Write-Host "  [PASS] tag $version" -ForegroundColor Green }
    else { Write-Host "  [FAIL] tag $version  commit=$($localHead.Substring(0,7)) tag=$($tagHead.Substring(0,7))" -ForegroundColor Red; $ok = $false }

    if (Test-Path $scZip) {
        $localSC = (Get-FileHash $scZip -Algorithm SHA256).Hash
        $remoteAssets = & gh release view $version --repo Gosolceser1/Asset-Splitter-UI --json assets 2>$null | ConvertFrom-Json
        if ($remoteAssets) {
            $remoteSC = ($remoteAssets.assets | Where-Object { $_.name -eq (Split-Path $scZip -Leaf) }).digest -replace 'sha256:',''
            if ($localSC -eq $remoteSC) { Write-Host "  [PASS] self-contained zip" -ForegroundColor Green }
            else { Write-Host "  [FAIL] self-contained zip" -ForegroundColor Red; $ok = $false }
        }
    }
    if (Test-Path $fwZip) {
        $localFW = (Get-FileHash $fwZip -Algorithm SHA256).Hash
        $remoteFW = ($remoteAssets.assets | Where-Object { $_.name -eq (Split-Path $fwZip -Leaf) }).digest -replace 'sha256:',''
        if ($localFW -eq $remoteFW) { Write-Host "  [PASS] framework zip" -ForegroundColor Green }
        else { Write-Host "  [FAIL] framework zip" -ForegroundColor Red; $ok = $false }
    }

    if (-not $ok) { $buildSuccess = $false }
}

# Reset NUGET_PACKAGES to system default now that all build/publish steps are done.
$env:NUGET_PACKAGES = $null

# Final summary: total elapsed time and aggregated warning/error counts across all steps.
$scriptStart.Stop()
$totalTime = [math]::Round($scriptStart.Elapsed.TotalSeconds, 1)

Write-Host ""
Write-Host "----------------------------------------------------------------------" -ForegroundColor DarkGray

if ($buildSuccess) {
    Write-Host "  [OK] All builds completed successfully! ( $totalTime seconds )" -ForegroundColor Green
    if ($script:totalWarnings -gt 0) {
        Write-Host "    Total warnings: $($script:totalWarnings)" -ForegroundColor Yellow
    }
    Write-Host ""
    exit 0
} else {
    Write-Host "  [FAIL] Build failed with $($script:totalErrors) error(s)" -ForegroundColor Red
    Write-Host ""
    exit 1
}


