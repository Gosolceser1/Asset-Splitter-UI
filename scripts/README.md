# Scripts Directory

Quick-reference for PowerShell scripts in this folder — build, release, CLI extraction, and utilities.

**Also see:** [docs/README.md](../docs/README.md)

---

## Quick Decision Guide

| You want to... | Use |
|---|---|
| Build after code changes (fast) | `.\scripts\build.ps1 -Fast` |
| Full clean rebuild + release zips | `.\scripts\build.ps1` |
| Build + publish + push + GitHub release | `.\scripts\build.ps1 -CreateRelease` |
| Debug backend pipeline output | `.\scripts\debug-run.ps1` |
| Extract Anno 1800 assets (CLI) | `.\scripts\anno1800-extract.ps1` |
| Extract Anno 117 assets (CLI) | `.\scripts\anno117-extract.ps1` |
| Push fresh commit to GitHub | `.\scripts\build.ps1 -CreateRelease` (or `squash-history-and-push.ps1`) |
| Convert PNG to ICO icon | `.\scripts\png-to-ico.ps1` |

---

## Build Scripts

### `build.ps1` — Primary build & release script

**One script for everything.** Replaces `clean-build.ps1`, `fast-build.ps1`, and `squash-history-and-push.ps1` with a single consolidated entry point.

**Modes:**

| Flag | What it does |
|---|---|
| *(none)* | Full clean: shutdown build servers, kill stale processes, delete bin/obj, clear NuGet cache, restore, build Debug + Release, publish zips |
| `-Fast` | Quick incremental Debug build — no clean, no NuGet clear, no publish. For daily dev. |
| `-CreateRelease` | Full clean → build → publish → squash-push to GitHub → update tag → upload release zips |
| `-DebugOnly` | Only Debug; skip Release and publish |
| `-ReleaseOnly` | Only Release |
| `-SkipPublish` | Build but don't publish or zip |
| `-SkipClean` | Skip bin/obj deletion |
| `-SkipNuGetClear` | Keep NuGet cache |
| `-Verbose` | Full dotnet output |

```powershell
.\scripts\build.ps1                         # Full clean build + publish zips
.\scripts\build.ps1 -Fast                   # Quick incremental Debug build
.\scripts\build.ps1 -CreateRelease          # Build + publish + push + GitHub release
.\scripts\build.ps1 -Fast -DebugOnly        # Quick debug-only
.\scripts\build.ps1 -SkipPublish            # Build without zips
.\scripts\build.ps1 -CreateRelease          # Full release pipeline
```

**Output**: `Asset-Splitter-UI-v1.0.0-win-x64-self-contained.zip` and `Asset-Splitter-UI-v1.0.0-win-x64-framework.zip` in repo root.

---

### `build-common.ps1` — Shared helper functions

**Library file** — not run directly. Sourced by `build.ps1`, `clean-build.ps1`, `fast-build.ps1`, `debug-run.ps1`, and `squash-history-and-push.ps1`.

| Function | Purpose |
|---|---|
| `Write-Step` / `Write-StepHeader` | Colored section dividers |
| `Write-Status` | Timestamped `[HH:MM:SS] [STATUS] message` |
| `Write-Elapsed` | Human-readable time (`2m 15s`) |
| `Write-FileCount` | Folder stats (`12 files in 3 folders (4.2 MB)`) |
| `Get-SolutionPath` / `Get-ProjectRoot` | Path resolution |
| `Get-UIProject` / `Get-VersionFromCsproj` | UI project path + version |
| `Invoke-DotnetBuild` | Build with timing, warnings, error output |
| `Assert-SolutionExists` | Validate `.sln` file |

---

### Legacy Scripts

These scripts still work but `build.ps1` is the recommended replacement:

| Old script | Replacement |
|---|---|
| `clean-build.ps1` | `build.ps1` (same flags: `-SkipPublish`, `-DebugOnly`, `-CreateRelease`) |
| `fast-build.ps1` | `build.ps1 -Fast` |
| `squash-history-and-push.ps1` | `build.ps1 -CreateRelease` |

---

## Extraction Scripts

These scripts run the backend CLI (`dotnet run --project src/AssetSplitter`) directly — bypassing the GUI. Useful for testing, automation, and debugging.

### `extract-common.ps1` — Shared extraction helpers

**Library file.** Sourced by `anno117-extract.ps1`, `anno1800-extract.ps1`, and `debug-run.ps1`.

---

### `anno1800-extract.ps1` — Extract Anno 1800 assets via CLI

```powershell
.\scripts\anno1800-extract.ps1                                    # Auto-detect game
.\scripts\anno1800-extract.ps1 -GamePath "C:\Games\Anno 1800"     # Custom path
.\scripts\anno1800-extract.ps1 -Language german                   # German GUID comments
.\scripts\anno1800-extract.ps1 -Output "C:\MyMods"                # Custom output folder
```

**Flags**: `-c` (comments), `-f` (dependencies), `-t` (template folders), `-y` (overwrite).

**Output**: `{Documents|Custom}/AssetSplit_Output/AnnoAssets/Anno1800/`

---

### `anno117-extract.ps1` — Extract Anno 117 assets via CLI

```powershell
.\scripts\anno117-extract.ps1                                     # Auto-detect game
.\scripts\anno117-extract.ps1 -GamePath "D:\Steam\Anno 117"       # Steam path
.\scripts\anno117-extract.ps1 -Language french                    # French GUID comments
.\scripts\anno117-extract.ps1 -CreateAssetMods                    # Also create mod packages
```

**Flags**: `-c -f -t -y` + optional `-CreateAssetMods`.

**Output**: `{Documents|Custom}/AssetSplit_Output/AnnoAssets/Anno117/`

---

### `debug-run.ps1` — Debug backend pipeline

**When**: Troubleshooting extraction — enables `-d` (DebugMode). Shows every pipeline decision.

```powershell
.\scripts\debug-run.ps1                                          # Auto-detect game
.\scripts\debug-run.ps1 -Game anno117                            # Force Anno 117
.\scripts\debug-run.ps1 -Guid 1010291                            # Single asset (Bakery)
.\scripts\debug-run.ps1 -AddComments -TemplateFolders            # Specific features
```

**Output**: `debug-output/` in repo root (gitignored). Includes `debug-log.txt` and `debug-summary.txt`.

---

## Git Scripts

### `squash-history-and-push.ps1` — Standalone squash-push

**When**: You only need to push to GitHub without building. For the full pipeline use `build.ps1 -CreateRelease`.

```powershell
.\scripts\squash-history-and-push.ps1                  # Interactive confirm
.\scripts\squash-history-and-push.ps1 -Force            # No confirmation
```

---

## Utility Scripts

### `png-to-ico.ps1` — Convert app icon PNG to ICO

```powershell
.\scripts\png-to-ico.ps1
```

Converts `src/AssetSplitterUI/Assets/app-icon.png` to `app-icon.ico`. Uses C# PngToIco tool (multi-size) with .NET fallback.

---

## Agent Tools (`agent-tools/`)

**For AI coding assistants and RDA investigation** — not end-user tools.

| File | Purpose |
|---|---|
| `RDA-Agent.ps1` | Game switching, `rda-find`, `rda-list`, `rda-read`, `rda-cheats` |
| `Read-RDA.ps1` | Low-level RDA V2.2 library: parse, decrypt, decompress |
| `RDA-KNOWLEDGE.md` | Verified archive maps (29 Anno 117 + 36 Anno 1800) |
| `texconv.exe` | DirectX texture converter |

---

## File Relationships

```
build.ps1  (primary entry point — self-contained, no dependencies)

build-common.ps1 ──────→ clean-build.ps1 (legacy)
                   └───→ fast-build.ps1 (legacy)
                   └───→ squash-history-and-push.ps1 (legacy)
                   └───→ debug-run.ps1

extract-common.ps1 ────→ anno117-extract.ps1
                    └──→ anno1800-extract.ps1
                    └──→ debug-run.ps1

PngToIco/ (C# project) ←── png-to-ico.ps1
```

---

## Output Locations

| Script | Output Path |
|---|---|
| `build.ps1` | `Asset-Splitter-UI-v*.zip` in repo root |
| `debug-run.ps1` | `debug-output/` in repo root (gitignored) |
| `anno1800-extract.ps1` | `Documents/AssetSplit_Output/AnnoAssets/Anno1800/` |
| `anno117-extract.ps1` | `Documents/AssetSplit_Output/AnnoAssets/Anno117/` |
| `png-to-ico.ps1` | `src/AssetSplitterUI/Assets/app-icon.ico` |
