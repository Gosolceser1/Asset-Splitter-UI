<#
.SYNOPSIS
    RDA investigation + quick-access tool for Anno 117 and Anno 1800.

.DESCRIPTION
    Dot-source this file, select a game, then use the short aliases to explore
    and investigate RDA archives without needing to remember full paths.

    On load the script auto-detects whichever game(s) are installed and
    activates one automatically. If both are present it asks you to pick.

    Anno 117  — archives are in <game>\maindata\*.rda
                (config.rda, shared_configs.rda, ui.rda, textures.rda, ...)
    Anno 1800 — archives are in <game>\maindata\*.rda
                (data0.rda … data33.rda;  higher number = newer patch/DLC,
                 overrides files from lower-numbered archives)

.NOTES
    This script is best-effort tooling for quick RDA investigation. It implements a
    subset of the RDA format via Read-RDA.ps1 with narrower assumptions than the
    production C# parser. For full RDA extraction and validation, prefer the C# API:
    src/RDAExplorer/RDAReader.cs + RDAFileExtension.ExtractAll().

.EXAMPLE
    # Load + auto-activate (or pick manually afterwards)
    . .\RDA-Agent.ps1

    # Find which archive(s) contain something — the main investigation command
    rda-find "datasets"           # → tells you which RDA and the exact path
    rda-find "BlacklistFeature"   # → works for any partial path fragment
    rda-grep "GoodNatureBuff"     # → deep text search across ALL archives

    # Once you know where something is
    rda-list data28 "datasets"    # confirm exact path inside that archive
    rda-read data28 "data/config/export/main/asset/datasets.xml"

.EXAMPLE
    # Switch games mid-session
    Use-Anno117
    rda-find "texts_english"

    Use-Anno1800
    rda-find "texts_english"
#>

# ─────────────────────────────────────────────────────────────────────────────
# Bootstrap
# ─────────────────────────────────────────────────────────────────────────────
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. "$scriptDir\Read-RDA.ps1"

$Script:GamePath    = ""
$Script:MainData    = ""
$Script:GameName    = ""
$Script:TexconvPath = Join-Path $scriptDir "texconv.exe"
$Script:RdaFileListCache = @{}

function Resolve-GamePathInfo {
    <#
    .SYNOPSIS
        Resolve either a game root path or a maindata path into canonical paths.
    .DESCRIPTION
        Accepts:
        - <game>\            (contains maindata\)
        - <game>\maindata\   (contains *.rda)
        Returns a PSCustomObject with GamePath/MainData or $null if invalid.
    #>
    param([Parameter(Mandatory=$true)][string]$Path)

    if (-not (Test-Path $Path)) { return $null }

    $resolved = (Resolve-Path $Path).Path
    if (-not (Test-Path $resolved -PathType Container)) { return $null }

    $isMainDataFolder = [System.IO.Path]::GetFileName($resolved).ToLowerInvariant() -eq "maindata"
    if ($isMainDataFolder) {
        $hasRdas = @(Get-ChildItem -Path $resolved -Filter "*.rda" -ErrorAction SilentlyContinue).Count -gt 0
        if ($hasRdas) {
            return [PSCustomObject]@{
                GamePath = Split-Path -Parent $resolved
                MainData = $resolved
            }
        }
    }

    $mainData = Join-Path $resolved "maindata"
    if (Test-Path $mainData -PathType Container) {
        $hasRdas = @(Get-ChildItem -Path $mainData -Filter "*.rda" -ErrorAction SilentlyContinue).Count -gt 0
        if ($hasRdas) {
            return [PSCustomObject]@{
                GamePath = $resolved
                MainData = $mainData
            }
        }
    }

    return $null
}

function Get-SteamCommonRoots {
    <#
    .SYNOPSIS
        Discover Steam common-library roots from registry + libraryfolders.vdf.
    #>
    $roots = [System.Collections.Generic.List[string]]::new()

    foreach ($regPath in @(
        "HKCU:\SOFTWARE\Valve\Steam",
        "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam",
        "HKLM:\SOFTWARE\Valve\Steam"
    )) {
        try {
            $reg = Get-ItemProperty -Path $regPath -ErrorAction Stop
            foreach ($prop in @("SteamPath", "InstallPath")) {
                if ($reg.$prop -and (Test-Path $reg.$prop)) {
                    $common = Join-Path $reg.$prop "steamapps\common"
                    if (Test-Path $common) { [void]$roots.Add($common) }

                    $librariesVdf = Join-Path $reg.$prop "steamapps\libraryfolders.vdf"
                    if (Test-Path $librariesVdf) {
                        foreach ($line in (Get-Content $librariesVdf -ErrorAction SilentlyContinue)) {
                            if ($line -match '"path"\s+"([^"]+)"') {
                                $libRoot = $Matches[1].Replace('\\\\', '\\')
                                $libCommon = Join-Path $libRoot "steamapps\common"
                                if (Test-Path $libCommon) { [void]$roots.Add($libCommon) }
                            }
                        }
                    }
                }
            }
        }
        catch {
            # Best-effort discovery only.
        }
    }

    foreach ($fallback in @(
        "C:\Program Files (x86)\Steam\steamapps\common",
        "C:\Program Files\Steam\steamapps\common",
        "D:\Steam\steamapps\common",
        "E:\Steam\steamapps\common",
        "$env:USERPROFILE\.steam\steam\steamapps\common"
    )) {
        if (Test-Path $fallback) { [void]$roots.Add($fallback) }
    }

    return @($roots | Select-Object -Unique)
}

function Find-InstalledGamePath {
    <#
    .SYNOPSIS
        Find a real installed game path and validate it contains maindata/*.rda.
    #>
    param(
        [Parameter(Mandatory=$true)][string]$GameTag,
        [Parameter(Mandatory=$true)][string[]]$StaticCandidates
    )

    $dynamicCandidates = [System.Collections.Generic.List[string]]::new()
    foreach ($root in (Get-SteamCommonRoots)) {
        if ($GameTag -eq "Anno117") {
            foreach ($name in @("Anno 117 - Pax Romana", "Anno 117")) {
                [void]$dynamicCandidates.Add((Join-Path $root $name))
            }
        }
        elseif ($GameTag -eq "Anno1800") {
            [void]$dynamicCandidates.Add((Join-Path $root "Anno 1800"))
        }
    }

    if ($GameTag -eq "Anno1800") {
        foreach ($ubiRoot in @(
            "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800",
            "C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 1800"
        )) {
            [void]$dynamicCandidates.Add($ubiRoot)
        }
    }

    $allCandidates = @($StaticCandidates + @($dynamicCandidates | Select-Object -Unique))
    foreach ($candidate in $allCandidates) {
        $info = Resolve-GamePathInfo -Path $candidate
        if ($null -ne $info) {
            return $info
        }
    }

    return $null
}

# Common install locations probed when auto-detecting
$Script:Anno117Candidates = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana",
    "D:\Steam\steamapps\common\Anno 117 - Pax Romana",
    "E:\Steam\steamapps\common\Anno 117 - Pax Romana",
    "$env:USERPROFILE\.steam\steam\steamapps\common\Anno 117 - Pax Romana"
)
$Script:Anno1800Candidates = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Anno 1800",
    "D:\Steam\steamapps\common\Anno 1800",
    "E:\Steam\steamapps\common\Anno 1800",
    "$env:USERPROFILE\.steam\steam\steamapps\common\Anno 1800",
    "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800",
    "C:\Program Files\Ubisoft\Ubisoft Game Launcher\games\Anno 1800"
)

# ─────────────────────────────────────────────────────────────────────────────
# Game switching
# ─────────────────────────────────────────────────────────────────────────────

function Use-Anno117 {
    <#
    .SYNOPSIS  Activate Anno 117 as the current game context.
    .PARAMETER Path  Optional manual install path (auto-detected otherwise).
    #>
    param([string]$Path = "")
    if ($Path) {
        $resolved = Resolve-GamePathInfo -Path $Path
        if ($null -eq $resolved) {
            throw "Anno 117 path invalid. Provide game root or maindata path containing *.rda"
        }
    }
    else {
        $resolved = Find-InstalledGamePath -GameTag "Anno117" -StaticCandidates $Script:Anno117Candidates
        if ($null -eq $resolved) {
            throw "Anno 117 not found. Run: Use-Anno117 -Path 'C:\your\path'"
        }
    }
    $Script:GamePath = $resolved.GamePath
    $Script:MainData = $resolved.MainData
    $Script:GameName = "Anno117"
    $Script:RdaFileListCache = @{}
    Write-Host "Active: Anno 117 — $Script:MainData" -ForegroundColor Cyan
}

function Use-Anno1800 {
    <#
    .SYNOPSIS  Activate Anno 1800 as the current game context.
    .PARAMETER Path  Optional manual install path (auto-detected otherwise).
    #>
    param([string]$Path = "")
    if ($Path) {
        $resolved = Resolve-GamePathInfo -Path $Path
        if ($null -eq $resolved) {
            throw "Anno 1800 path invalid. Provide game root or maindata path containing *.rda"
        }
    }
    else {
        $resolved = Find-InstalledGamePath -GameTag "Anno1800" -StaticCandidates $Script:Anno1800Candidates
        if ($null -eq $resolved) {
            throw "Anno 1800 not found. Run: Use-Anno1800 -Path 'C:\your\path'"
        }
    }
    $Script:GamePath = $resolved.GamePath
    $Script:MainData = $resolved.MainData
    $Script:GameName = "Anno1800"
    $Script:RdaFileListCache = @{}
    Write-Host "Active: Anno 1800 — $Script:MainData" -ForegroundColor Cyan
}

function Assert-GameContext {
    if (-not $Script:MainData -or -not (Test-Path $Script:MainData)) {
        throw "No game active. Run  Use-Anno117  or  Use-Anno1800  first."
    }
}

function Get-CurrentGame {
    <# Show which game is currently active. #>
    if (-not $Script:GameName) {
        Write-Host "No game active. Run  Use-Anno117  or  Use-Anno1800." -ForegroundColor Yellow
    } else {
        Write-Host "Active: $Script:GameName  →  $Script:MainData" -ForegroundColor Cyan
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Internal helpers
# ─────────────────────────────────────────────────────────────────────────────

function Resolve-RdaPath {
    param([string]$Name)
    $p = Join-Path $Script:MainData $Name
    if (-not $p.EndsWith(".rda")) { $p += ".rda" }
    return $p
}

function Get-RdaFileListCached {
    <# Return file list from cache (per archive) to avoid repeated heavy scans. #>
    param(
        [Parameter(Mandatory=$true)][string]$RdaPath,
        [string]$Filter = ""
    )

    if (-not $Script:RdaFileListCache.ContainsKey($RdaPath)) {
        $Script:RdaFileListCache[$RdaPath] = @(Get-RDAFileList -Path $RdaPath)
    }

    $all = $Script:RdaFileListCache[$RdaPath]
    if (-not $Filter) { return $all }
    return @($all | Where-Object { $_ -match $Filter })
}

function Get-AuthoritativeRdaForPattern {
    <#
    .SYNOPSIS
        Resolve the authoritative archive (highest-priority) containing a path pattern.
    .DESCRIPTION
        For Anno 1800, archive order is already highest dataXX first.
        For Anno 117, archive order is by name; core files live in named archives.
    #>
    param([Parameter(Mandatory=$true)][string]$PathPattern)
    Assert-GameContext

    foreach ($rda in (Get-AllRdaFilesInContext)) {
        $hits = @(Get-RdaFileListCached -RdaPath $rda.FullName -Filter $PathPattern)
        if ($hits -and $hits.Count -gt 0) {
            return [PSCustomObject]@{
                Archive = $rda
                FilePath = $hits[0]
                AllHits = $hits
            }
        }
    }
    return $null
}

function Get-AuthoritativePathMatches {
    <#
    .SYNOPSIS
        Return authoritative path matches across all archives (override-aware).
    .DESCRIPTION
        Iterates archives in authoritative order and keeps only the first
        occurrence per internal file path.
    #>
    param([Parameter(Mandatory=$true)][string]$PathPattern)
    Assert-GameContext

    $seen = @{}
    $results = [System.Collections.ArrayList]::new()

    foreach ($rda in (Get-AllRdaFilesInContext)) {
        $hits = @(Get-RdaFileListCached -RdaPath $rda.FullName -Filter $PathPattern)
        foreach ($hit in $hits) {
            if (-not $seen.ContainsKey($hit)) {
                $seen[$hit] = $true
                [void]$results.Add([PSCustomObject]@{
                    Archive = $rda
                    FilePath = $hit
                })
            }
        }
    }

    return @($results)
}

# Returns all *.rda files in the current context, sorted appropriately.
# Anno 1800: sorted numerically by data number, highest (newest) first.
# Anno 117:  sorted by name.
function Get-AllRdaFilesInContext {
    Assert-GameContext
    $files = Get-ChildItem $Script:MainData -Filter "*.rda"
    if ($Script:GameName -eq "Anno1800") {
        $files | Sort-Object {
            if ($_.BaseName -match '^data(\d+)$') { [int]$Matches[1] } else { -1 }
        } -Descending
    } else {
        $files | Sort-Object Name
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Noise exclusion filters — paths known to produce false positives
# ─────────────────────────────────────────────────────────────────────────────

# Anno 1800 paths to exclude from meaningful searches (tens of thousands of files
# that match common keywords like "console" / "debug" but are NOT game logic).
$Script:Anno1800NoisePatterns = @(
    "data/ui/2kimages/"          # ALL Xbox/PS/PC UI icon textures (100k+ .dds files)
    "data/ui/studio/"            # Rendered UI scene images
    "data/ui/backgrounds/"       # Background images
    "data/graphics/"             # 3D graphics / normal-map textures
    "data/script/lib/"           # Python stdlib (distutils, idlelib, setuptools, etc.)
    "data/script/pydevd/"        # PyCharm remote debugger test files
    "data/sound/"                # Audio banks
    "data/shaders/"              # Shader cache
    ".dds"                       # Any remaining DDS image file
    "en_us0.rda"                 # Locale audio archive
    "ru_ru0.rda"                 # Locale audio archive
)

# Returns $true if the given file path should be excluded from results.
function Test-IsNoisePath {
    param([string]$FilePath, [string]$RdaName)
    if ($Script:GameName -eq "Anno1800") {
        foreach ($noise in $Script:Anno1800NoisePatterns) {
            if ($FilePath -like "*$noise*" -or $RdaName -eq $noise) { return $true }
        }
    }
    return $false
}

# ─────────────────────────────────────────────────────────────────────────────
# ★ INVESTIGATION commands — search ACROSS ALL archives
# ─────────────────────────────────────────────────────────────────────────────

function Find-InAllRdas {
    <#
    .SYNOPSIS
        Find which RDA archive(s) contain file paths matching a pattern.
    .DESCRIPTION
        The main investigation command. Scans every archive in the current game
        and reports which ones contain at least one file path matching the regex.

        Anno 1800 results are shown highest-numbered (newest) first so the
        authoritative version of overridden files appears at the top.
        Noise paths (console UI icons, Python stdlib, audio) are excluded by
        default. Use -IncludeNoise to see everything.
    .PARAMETER Pattern
        Regex to match against internal file paths.
    .PARAMETER IncludeNoise
        Include known noise paths (console port icons, stdlib, audio).
    .EXAMPLE
        rda-find "datasets"
        rda-find "texts_english"
        rda-find "assets\.xml"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [switch]$IncludeNoise
    )
    Assert-GameContext

    $rdaFiles = Get-AllRdaFilesInContext
    $totalFound = 0
    $totalNoise = 0

    foreach ($rda in $rdaFiles) {
        if (-not $IncludeNoise -and (Test-IsNoisePath -FilePath "" -RdaName $rda.Name)) {
            continue
        }

        try {
            $hits = @(Get-RdaFileListCached -RdaPath $rda.FullName -Filter $Pattern)

            if ($hits.Count -gt 0 -and -not $IncludeNoise) {
                $filtered = @($hits | Where-Object { -not (Test-IsNoisePath -FilePath $_ -RdaName $rda.Name) })
                $totalNoise += ($hits.Count - $filtered.Count)
                $hits = $filtered
            }

            if ($hits.Count -gt 0) {
                Write-Host "`n  [$($rda.Name)]" -ForegroundColor Yellow
                $hits | ForEach-Object { Write-Host "    $_" -ForegroundColor White }
                $totalFound += $hits.Count
            }
        }
        catch {
            Write-Host "  [$($rda.Name)]  skipped: $_" -ForegroundColor DarkGray
        }
    }

    if ($totalFound -eq 0) {
        Write-Host "No matches for '$Pattern' in any archive." -ForegroundColor Red
    }
    else {
        Write-Host "`n  Total: $totalFound file(s) found." -ForegroundColor DarkGray
    }

    if ($totalNoise -gt 0) {
        Write-Host "  ($totalNoise noise file(s) suppressed — use -IncludeNoise to see all)" -ForegroundColor DarkGray
    }
}

function Search-InAllRdas {
    <#
    .SYNOPSIS
        Search text content across every RDA in the current game.
    .DESCRIPTION
        Opens every archive, reads text files (xml/txt/lua/json/ini) and
        reports regex matches with context. Slower than rda-find — prefer
        rda-find for locating files, use rda-grep when you need to find
        a string that could be inside any file.
        Noise paths (console UI icons, Python stdlib, audio) are excluded.
    .PARAMETER Pattern
        Regex to search inside file contents.
    .PARAMETER FileFilter
        Regex filter on file paths to limit which files are read.
        Default: xml, txt, lua, json, ini, py.
    .EXAMPLE
        rda-grep "GoodNatureBuff"
        rda-grep "SomeGUID" "\.xml$"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Pattern,
        [string]$FileFilter = "\.(xml|txt|lua|json|ini|py)$",
        [switch]$IncludeNoise
    )
    Assert-GameContext

    $rdaFiles = Get-AllRdaFilesInContext
    $totalFound = 0

    foreach ($rda in $rdaFiles) {
        if (-not $IncludeNoise -and (Test-IsNoisePath -FilePath "" -RdaName $rda.Name)) {
            continue
        }

        Write-Host "  Scanning $($rda.Name)..." -ForegroundColor DarkGray -NoNewline

        try {
            $results = @(Search-RDAContent -Path $rda.FullName -Pattern $Pattern -FileFilter $FileFilter)

            if ($results.Count -gt 0 -and -not $IncludeNoise) {
                $results = @($results | Where-Object { -not (Test-IsNoisePath -FilePath $_.File -RdaName $rda.Name) })
            }

            if ($results.Count -gt 0) {
                Write-Host "  $($results.Count) match(es)" -ForegroundColor Yellow
                $results | Format-Table -AutoSize File, Match, Context
                $totalFound += $results.Count
            }
            else {
                Write-Host ""
            }
        }
        catch {
            Write-Host "  error: $_" -ForegroundColor DarkGray
        }
    }

    if ($totalFound -eq 0) {
        Write-Host "No text matches for '$Pattern'." -ForegroundColor Red
    }
}



# ─────────────────────────────────────────────────────────────────────────────
# Single-archive commands (all use short archive name via $Script:MainData)
# ─────────────────────────────────────────────────────────────────────────────

function Get-RdaArchiveInfo {
    <# List all RDA archives in the current game with file/block counts. #>
    Assert-GameContext
    Get-AllRdaFilesInContext | ForEach-Object { Get-RDAInfo -Path $_.FullName }
}

function Get-RdaArchiveInfoSingle {
    <# Show metadata for a single archive (size, blocks, file count). #>
    param([Parameter(Mandatory=$true)][string]$Name)
    Assert-GameContext
    Get-RDAInfo -Path (Resolve-RdaPath $Name)
}

function Get-RdaArchiveBlocks {
    <# List all blocks inside an archive with flags and sizes. #>
    param([Parameter(Mandatory=$true)][string]$Name)
    Assert-GameContext
    Get-RDABlocks -Path (Resolve-RdaPath $Name)
}

function Get-RdaArchiveList {
    <#
    .SYNOPSIS  List files inside a single archive.
    .PARAMETER Name    Short archive name — e.g. config, data28, ui
    .PARAMETER Filter  Optional regex to filter the file list.
    .EXAMPLE   rda-list config
    .EXAMPLE   rda-list data28 "assets"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [string]$Filter = ""
    )
    Assert-GameContext
    if ($Filter) {
        Get-RDAFileList -Path (Resolve-RdaPath $Name) -Filter $Filter
    } else {
        Get-RDAFileList -Path (Resolve-RdaPath $Name)
    }
}

function Get-RdaArchiveFile {
    <#
    .SYNOPSIS  Read a file's contents from an archive.
    .PARAMETER Name      Short archive name — e.g. config, data28
    .PARAMETER FileName  Full internal path, e.g. data/base/config/export/assets.xml
    .PARAMETER AsBytes   Return raw bytes instead of text.
    .EXAMPLE   rda-read config "data/base/config/export/assets.xml"
    .EXAMPLE   rda-read data28 "data/config/export/main/asset/datasets.xml" | Out-File datasets.xml
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$FileName,
        [switch]$AsBytes
    )
    Assert-GameContext
    Read-RDAFile -Path (Resolve-RdaPath $Name) -FileName $FileName -AsBytes:$AsBytes
}

function Find-RdaArchiveContent {
    <#
    .SYNOPSIS  Search text content inside a single archive.
    .PARAMETER Name    Short archive name.
    .PARAMETER Pattern Regex to search for.
    .EXAMPLE   rda-search config "BlacklistFeature"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Pattern,
        [string]$FileFilter = "\.(xml|txt|lua|json|ini)$",
        [int]$Context = 80
    )
    Assert-GameContext
    Search-RDAContent -Path (Resolve-RdaPath $Name) -Pattern $Pattern -FileFilter $FileFilter -Context $Context
}

function Export-RdaArchiveFile {
    <#
    .SYNOPSIS  Extract a file from an archive to disk.
    .PARAMETER Name        Short archive name.
    .PARAMETER FileName    Full internal path.
    .PARAMETER OutputPath  Where to save (defaults to current directory).
    .EXAMPLE   rda-extract config "data/base/config/export/assets.xml" ".\assets.xml"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$FileName,
        [string]$OutputPath = ""
    )
    Assert-GameContext
    if (-not $OutputPath) {
        $OutputPath = Join-Path (Get-Location) ([System.IO.Path]::GetFileName($FileName))
    }
    $bytes = Read-RDAFile -Path (Resolve-RdaPath $Name) -FileName $FileName -AsBytes
    [System.IO.File]::WriteAllBytes($OutputPath, $bytes)
    Write-Host "Extracted to: $OutputPath"
}

function Convert-RdaArchiveImage {
    <#
    .SYNOPSIS  Extract a DDS image from an archive and convert it to PNG (or other format).
    .PARAMETER Name      Short archive name.
    .PARAMETER FileName  Full internal path to the .dds file.
    .PARAMETER Format    Output format (default: png). Options: png, bmp, jpg, tga, tif.
    .PARAMETER OutputPath Where to save. Defaults to current directory.
    .EXAMPLE   rda-image ui "data/ui/textures/icon.dds"
    #>
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$FileName,
        [string]$Format = "png",
        [string]$OutputPath = ""
    )
    Assert-GameContext
    if (-not (Test-Path $Script:TexconvPath)) {
        throw "texconv.exe not found at: $Script:TexconvPath`nDownload from: https://github.com/microsoft/DirectXTex/releases"
    }
    if (-not $OutputPath) {
        $base = [System.IO.Path]::GetFileNameWithoutExtension($FileName)
        $OutputPath = Join-Path (Get-Location) "$base.$Format"
    }
    $outputDir  = Split-Path $OutputPath
    $tempDir    = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "rda-img-" + [System.Guid]::NewGuid().ToString("N"))
    $outputFullPath = $OutputPath
    [System.IO.Directory]::CreateDirectory($tempDir) | Out-Null
    $tempInput  = Join-Path $tempDir ([System.IO.Path]::GetFileName($FileName))
    try {
        $bytes = Read-RDAFile -Path (Resolve-RdaPath $Name) -FileName $FileName -AsBytes
        [System.IO.File]::WriteAllBytes($tempInput, $bytes)
        & $Script:TexconvPath @("-y", "-ft", $Format, "-o", $outputDir, $tempInput) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "texconv failed (exit $LASTEXITCODE)" }
        $converted = Join-Path $outputDir ([System.IO.Path]::GetFileNameWithoutExtension($tempInput) + "." + $Format)
        if ($converted -ne $outputFullPath) { Move-Item -Force $converted $outputFullPath }
        Write-Host "Converted to: $outputFullPath"
    } finally {
        if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Smart game-aware shortcut commands
# ─────────────────────────────────────────────────────────────────────────────

function Get-GameCheats {
    <#
    .SYNOPSIS
        Show all cheat system files and APIs for the active game.
    .DESCRIPTION
        Anno 1800: reads data/script/predefs/anno6.pypredef from data0.rda
                   and shows the Cheat class + Game.activateCheat API.
        Anno 117:  reads all cheat Lua files from script.rda — CCheatBindings,
                   CGlobalCheats, CCheatManager, and the actual cheat scripts.
    .EXAMPLE
        rda-cheats
    #>
    Assert-GameContext

    if ($Script:GameName -eq "Anno1800") {
        $predefLoc = Get-AuthoritativeRdaForPattern -PathPattern "data/script/predefs/anno6\.pypredef$"
        if ($null -eq $predefLoc) {
            throw "Cheat API predef not found in any Anno 1800 archive."
        }

        Write-Host "`n=== Anno 1800 Cheat System (Python) ===" -ForegroundColor Cyan
        Write-Host "Archive: $($predefLoc.Archive.Name) — $($predefLoc.FilePath)`n" -ForegroundColor DarkGray
        $api = Read-RDAFile -Path $predefLoc.Archive.FullName -FileName $predefLoc.FilePath
        Write-Host $api
        Write-Host "`nUsage: game.activateCheat(Cheat.NO_BUILDCOSTS, True)" -ForegroundColor Yellow
        Write-Host "Known cheat constants: NO_BUILDCOSTS = 0" -ForegroundColor Yellow

    } elseif ($Script:GameName -eq "Anno117") {
        $gcLoc = Get-AuthoritativeRdaForPattern -PathPattern "data/script/types/generated/rdgs/cglobalcheatsluabindings\.lua$"
        $cbLoc = Get-AuthoritativeRdaForPattern -PathPattern "data/script/types/generated/rdgs/ccheatbindingsluabindings\.lua$"
        $cheatLocs = Get-AuthoritativePathMatches -PathPattern "^data/script/content/cheats/.*\.lua$"

        Write-Host "`n=== Anno 117 Cheat System (Lua) ===" -ForegroundColor Cyan
        Write-Host "Cheats = CCheatBindings (session-scoped)" -ForegroundColor DarkGray
        Write-Host "Cheat  = CCheatManager  (global)" -ForegroundColor DarkGray
        Write-Host "Cheat.GlobalCheats = CGlobalCheats`n" -ForegroundColor DarkGray

        Write-Host "--- CGlobalCheats (toggle flags) ---" -ForegroundColor Yellow
        if ($null -eq $gcLoc) {
            Write-Host "  not found" -ForegroundColor DarkGray
        }
        else {
            Write-Host "  Source: $($gcLoc.Archive.Name) :: $($gcLoc.FilePath)" -ForegroundColor DarkGray
            $gc = Read-RDAFile -Path $gcLoc.Archive.FullName -FileName $gcLoc.FilePath
            ($gc -split "`n") | Where-Object { $_ -match "^---@field|^function CGlobalCheats\.(Toggle|Disable|Enable)" } | ForEach-Object { Write-Host "  $_" }
        }

        Write-Host "`n--- CCheatBindings (session cheats) ---" -ForegroundColor Yellow
        if ($null -eq $cbLoc) {
            Write-Host "  not found" -ForegroundColor DarkGray
        }
        else {
            Write-Host "  Source: $($cbLoc.Archive.Name) :: $($cbLoc.FilePath)" -ForegroundColor DarkGray
            $cb = Read-RDAFile -Path $cbLoc.Archive.FullName -FileName $cbLoc.FilePath
            ($cb -split "`n") | Where-Object { $_ -match "^function CCheatBindings\." } | ForEach-Object { Write-Host "  $_" }
        }

        Write-Host "`n--- Content Cheat Scripts ---" -ForegroundColor Yellow
        foreach ($loc in $cheatLocs) {
            $body = Read-RDAFile -Path $loc.Archive.FullName -FileName $loc.FilePath
            Write-Host "  $([System.IO.Path]::GetFileName($loc.FilePath))  [$($loc.Archive.Name)]" -ForegroundColor White
            Write-Host "    $body" -ForegroundColor Gray
        }
    }
}

function Get-GameAssets {
    <#
    .SYNOPSIS
        Read the authoritative assets.xml for the active game.
    .DESCRIPTION
        Anno 1800: reads from the highest-numbered data archive (newest patch).
        Anno 117:  reads from config.rda.
        Pipe to Select-String to filter: rda-assets | Select-String "Farmhouse"
    .EXAMPLE
        rda-assets
        rda-assets | Select-String "Template>Farmhouse"
    #>
    Assert-GameContext

    if ($Script:GameName -eq "Anno1800") {
        $best = Get-AuthoritativeRdaForPattern -PathPattern "data/config/export/main/asset/assets\.xml$"
        if ($null -eq $best) { throw "assets.xml not found in any archive." }
        Write-Host "Reading assets.xml from: $($best.Archive.Name)" -ForegroundColor DarkGray
        Read-RDAFile -Path $best.Archive.FullName -FileName $best.FilePath

    } elseif ($Script:GameName -eq "Anno117") {
        Write-Host "Reading assets.xml from: config.rda" -ForegroundColor DarkGray
        Read-RDAFile -Path (Join-Path $Script:MainData "config.rda") -FileName "data/base/config/export/assets.xml"
    }
}

function Get-GameTexts {
    <#
    .SYNOPSIS
        Read the English localisation texts for the active game.
    .DESCRIPTION
        Anno 1800: texts_english.xml from highest-numbered archive.
        Anno 117:  texts_english.xml from config.rda.
    .PARAMETER Language
        Language to read (default: english).
    .EXAMPLE
        rda-texts
        rda-texts german
        rda-texts | Select-String "10001"
    #>
    param([string]$Language = "english")
    Assert-GameContext

    if ($Script:GameName -eq "Anno1800") {
        $best = Get-AuthoritativeRdaForPattern -PathPattern "data/config/gui/texts_$Language\.xml$"
        if ($null -eq $best) { throw "texts_$Language.xml not found in any archive." }
        Write-Host "Reading texts_$Language.xml from: $($best.Archive.Name)" -ForegroundColor DarkGray
        Read-RDAFile -Path $best.Archive.FullName -FileName $best.FilePath

    } elseif ($Script:GameName -eq "Anno117") {
        Write-Host "Reading texts_$Language.xml from: config.rda" -ForegroundColor DarkGray
        Read-RDAFile -Path (Join-Path $Script:MainData "config.rda") -FileName "data/base/config/gui/texts_$Language.xml"
    }
}

function Get-GameScripts {
    <#
    .SYNOPSIS
        List game logic scripts for the active game.
    .DESCRIPTION
        Anno 1800: lists non-stdlib Python scripts from data0.rda.
                   (Excludes lib/ stdlib and pydevd/ debugger.)
        Anno 117:  lists all content Lua scripts from script.rda.
    .PARAMETER Filter
        Optional regex to filter the script list.
    .EXAMPLE
        rda-scripts
        rda-scripts "cheat"
    #>
    param([string]$Filter = "")
    Assert-GameContext

    if ($Script:GameName -eq "Anno1800") {
        $files = Get-AuthoritativePathMatches -PathPattern "^data/script/.*\.py$" |
            Where-Object { $_.FilePath -notlike "data/script/lib/*" -and
                           $_.FilePath -notlike "data/script/pydevd/*" }
        if ($Filter) { $files = $files | Where-Object { $_.FilePath -match $Filter } }
        Write-Host "Anno 1800 Python scripts (all archives, authoritative order, stdlib/debugger excluded):" -ForegroundColor DarkGray
        $files | Sort-Object FilePath | ForEach-Object { Write-Host "  $($_.Archive.Name) :: $($_.FilePath)" }

    } elseif ($Script:GameName -eq "Anno117") {
        $files = Get-AuthoritativePathMatches -PathPattern "^data/script/(content|core|module)/.*\.lua$"
        if ($Filter) { $files = $files | Where-Object { $_.FilePath -match $Filter } }
        Write-Host "Anno 117 Lua scripts (all archives, authoritative order):" -ForegroundColor DarkGray
        $files | Sort-Object FilePath | ForEach-Object { Write-Host "  $($_.Archive.Name) :: $($_.FilePath)" }
    }
}

function Get-GameDatasets {
    <#
    .SYNOPSIS
        Read the datasets.xml for the active game (balancing data).
    .DESCRIPTION
        Anno 1800: reads from the highest-numbered data archive (data28+).
        Anno 117:  reads from config.rda.
    .EXAMPLE
        rda-datasets
        rda-datasets | Select-String "PopulationLevel"
    #>
    Assert-GameContext

    if ($Script:GameName -eq "Anno1800") {
        $best = Get-AuthoritativeRdaForPattern -PathPattern "data/config/export/main/asset/datasets\.xml$"
        if ($null -eq $best) { throw "datasets.xml not found. Requires data28.rda or newer." }
        Write-Host "Reading datasets.xml from: $($best.Archive.Name)" -ForegroundColor DarkGray
        Read-RDAFile -Path $best.Archive.FullName -FileName $best.FilePath

    } elseif ($Script:GameName -eq "Anno117") {
        Write-Host "Reading datasets.xml from: config.rda" -ForegroundColor DarkGray
        Read-RDAFile -Path (Join-Path $Script:MainData "config.rda") -FileName "data/base/config/game/datasets.xml"
    }
}

function Get-GameLandmarks {
    <#
    .SYNOPSIS
        Show where key data lives for the active game.
    .DESCRIPTION
        Purpose-built to answer "where is X" quickly without guesswork.
        Outputs archive + exact internal path for core targets.
    .EXAMPLE
        rda-where
    #>
    Assert-GameContext

    Write-Host "`n=== $($Script:GameName) Landmarks ===" -ForegroundColor Cyan

    if ($Script:GameName -eq "Anno1800") {
        $targets = @(
            @{ Name = "assets.xml";    Pattern = "data/config/export/main/asset/assets\.xml$" },
            @{ Name = "templates.xml"; Pattern = "data/config/export/main/asset/templates\.xml$" },
            @{ Name = "properties.xml";Pattern = "data/config/export/main/asset/properties\.xml$" },
            @{ Name = "datasets.xml";  Pattern = "data/config/export/main/asset/datasets\.xml$" },
            @{ Name = "texts_english"; Pattern = "data/config/gui/texts_english\.xml$" },
            @{ Name = "python API";    Pattern = "data/script/predefs/anno6\.pypredef$" }
        )
    } else {
        $targets = @(
            @{ Name = "assets.xml";      Pattern = "data/base/config/export/assets\.xml$" },
            @{ Name = "templates.xml";   Pattern = "data/base/config/export/templates\.xml$" },
            @{ Name = "properties.xml";  Pattern = "data/base/config/export/properties\.xml$" },
            @{ Name = "datasets.xml";    Pattern = "data/base/config/game/datasets\.xml$" },
            @{ Name = "texts_english";   Pattern = "data/base/config/gui/texts_english\.xml$" },
            @{ Name = "cheat scripts";   Pattern = "data/script/content/cheats/" },
            @{ Name = "cheat bindings";  Pattern = "data/script/types/generated/rdgs/ccheatbindingsluabindings\.lua$" }
        )
    }

    foreach ($t in $targets) {
        $loc = Get-AuthoritativeRdaForPattern -PathPattern $t.Pattern
        if ($loc) {
            Write-Host ("  {0,-16} -> {1} :: {2}" -f $t.Name, $loc.Archive.Name, $loc.FilePath)
        } else {
            Write-Host ("  {0,-16} -> not found" -f $t.Name) -ForegroundColor Yellow
        }
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Aliases
# ─────────────────────────────────────────────────────────────────────────────
Set-Alias -Name rda-game117  -Value Use-Anno117
Set-Alias -Name rda-game1800 -Value Use-Anno1800
Set-Alias -Name rda-game     -Value Get-CurrentGame
Set-Alias -Name rda-all      -Value Get-RdaArchiveInfo
Set-Alias -Name rda-info     -Value Get-RdaArchiveInfoSingle
Set-Alias -Name rda-blocks   -Value Get-RdaArchiveBlocks
Set-Alias -Name rda-list     -Value Get-RdaArchiveList
Set-Alias -Name rda-read     -Value Get-RdaArchiveFile
Set-Alias -Name rda-search   -Value Find-RdaArchiveContent
Set-Alias -Name rda-extract  -Value Export-RdaArchiveFile
Set-Alias -Name rda-image    -Value Convert-RdaArchiveImage
Set-Alias -Name rda-find     -Value Find-InAllRdas      # search across all archives
Set-Alias -Name rda-grep     -Value Search-InAllRdas    # deep text search across all archives
Set-Alias -Name rda-cheats   -Value Get-GameCheats
Set-Alias -Name rda-assets   -Value Get-GameAssets
Set-Alias -Name rda-texts    -Value Get-GameTexts
Set-Alias -Name rda-scripts  -Value Get-GameScripts
Set-Alias -Name rda-datasets -Value Get-GameDatasets
Set-Alias -Name rda-where    -Value Get-GameLandmarks

# ─────────────────────────────────────────────────────────────────────────────
# Help banner
# ─────────────────────────────────────────────────────────────────────────────
Write-Host @"

╔══════════════════════════════════════════════════════════════════════════╗
║           RDA Investigation Tool  —  Anno 117  /  Anno 1800              ║
╠══════════════════════════════════════════════════════════════════════════╣
║  SELECT GAME FIRST                                                       ║
║    Use-Anno117   / rda-game117    auto-detect Anno 117                   ║
║    Use-Anno1800  / rda-game1800   auto-detect Anno 1800                  ║
║    Use-Anno117  -Path "D:\..."    specify path manually                  ║
║    (accepts game root OR maindata path with live *.rda files)            ║
║    rda-game                       show which game is active              ║
╠══════════════════════════════════════════════════════════════════════════╣
║  SMART COMMANDS  (game-aware, always go to the right place)              ║
║    rda-cheats                 show all cheats / cheat API for this game  ║
║    rda-assets [| grep X]      read authoritative assets.xml              ║
║    rda-texts  [lang]          read localisation texts (default: english) ║
║    rda-scripts [filter]       list game logic scripts                    ║
║    rda-datasets [| grep X]    read balancing datasets.xml                ║
║    rda-where                  show exact archive+path for key landmarks  ║
╠══════════════════════════════════════════════════════════════════════════╣
║  INVESTIGATE  (search across ALL archives — noise filtered by default)   ║
║    rda-find  <pattern>        which archive(s) contain a matching path?  ║
║    rda-find  <p> -IncludeNoise  include suppressed noise paths           ║
║    rda-grep  <pattern>        search text inside all archives (slower)   ║
╠══════════════════════════════════════════════════════════════════════════╣
║  EXPLORE  (single archive — use the short name without .rda)             ║
║    rda-all                list all archives with file/block counts       ║
║    rda-info  <n>          archive metadata                               ║
║    rda-list  <n> [filter] list files inside archive (regex filter)       ║
║    rda-read  <n> <file>   print file contents                            ║
║    rda-search <n> <text>  search text inside one archive                 ║
║    rda-extract <n> <file> extract file to disk                           ║
║    rda-image  <n> <file>  extract DDS image and convert to PNG           ║
╠══════════════════════════════════════════════════════════════════════════╣
║  QUICK EXAMPLE SESSIONS                                                  ║
║    Use-Anno1800                ║  Use-Anno117                            ║
║    rda-cheats                  ║  rda-cheats                             ║
║    rda-assets | grep Farm      ║  rda-datasets | grep PopLevel           ║
║    rda-find "datasets"         ║  rda-scripts "cheat"                    ║
╚══════════════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor DarkCyan

# ─────────────────────────────────────────────────────────────────────────────
# Auto-detect on load
# ─────────────────────────────────────────────────────────────────────────────
$a117  = Find-InstalledGamePath -GameTag "Anno117" -StaticCandidates $Script:Anno117Candidates
$a1800 = Find-InstalledGamePath -GameTag "Anno1800" -StaticCandidates $Script:Anno1800Candidates

if (($null -ne $a117) -and ($null -eq $a1800))       { Use-Anno117 -Path $a117.GamePath }
elseif (($null -ne $a1800) -and ($null -eq $a117))   { Use-Anno1800 -Path $a1800.GamePath }
elseif (($null -ne $a117) -and ($null -ne $a1800))   { Write-Host "  Both games detected. Run  Use-Anno117  or  Use-Anno1800  to activate one." -ForegroundColor DarkGray }
else                                                  { Write-Host "  No game installation found. Use  Use-Anno117 -Path 'C:\...'  or  Use-Anno1800 -Path 'C:\...'  to set path manually." -ForegroundColor Yellow }
