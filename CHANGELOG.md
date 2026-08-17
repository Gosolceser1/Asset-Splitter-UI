# Changelog

All notable changes to Asset Splitter UI are documented here.

---

## [1.0.0] — 2026-05-24

Initial public release — 211 files across 4 projects (RDAExplorer, AssetSplitter, RDAExtract, AssetSplitterUI).

### Features

**Extraction Pipeline**
- **Phase 1 — RDA extraction**: Decompresses game `.rda` archives, extracts `assets.xml`, `templates.xml`, `properties.xml` and all language files into a working folder
- **Phase 2 — Asset splitting**: Processes extracted XML into one ModOp XML file per asset with 8 configurable options

**Game Support**
- **Anno 1800**: Extracts from `data*.rda` archives; 5 core XML files + all language files
- **Anno 117 (Pax Romana)**: Extracts from `config.rda` / `shared_configs.rda`; 6 core XML files including `properties-meta.xml` and `audio_generated.xml`
- **Anno 117 Demo**: Detected and supported

**Processing Options** (all optional, toggled per run)
| Flag | Option | Description |
|------|--------|-------------|
| `-c` | Add GUID comments | Insert translated name comments next to GUID values |
| `-f` | Resolve BaseAssetGUID | Fill inherited properties from parent assets |
| `-t` | Organize by template folder | Group output into folders named by template (e.g. `FactoryBuilding7/`) |
| | ModOps wrap | Wrap each asset in `<ModOps><ModOp>` for direct mod use |
| | Apply default properties | Fill missing properties from `templates.xml` during merging |
| `--split-templates` | Split templates by template | Write one file per template into `output_templates_{game}/` |
| `--create-asset-mods` | Create mod packages by asset | Standalone Mod Loader folders under `mods/` with `modinfo.json` and README |
| `-d` | Debug mode | Verbose console logging for troubleshooting |

**Auto-Detection**
- Finds Anno installations via: **Ubisoft Connect** (registry), **Steam** (registry + library folders), **Epic Games** (manifest JSON)
- Scans drives C:/D:/E: for common paths
- Dropdown to switch between detected installations
- Per-game console state preservation (log, progress, phase remembered when switching)

**UI & UX**
- **11 UI languages**: English, Deutsch, Español, Français, Italiano, Polski, Русский, 中文, 日本語, 한국어, 繁體中文 (all 233 strings fully translated)
- **11 console message languages**: Same set, configurable independently of UI language
- **Theme switcher**: Light, Dark, or follow system (Auto)
- **Recent paths**: Remembers previously used game and output directories
- **Window size persistence**: Restores window dimensions on next launch
- **Dynamic button**: Automatically switches between "Extract Assets" / "Process Assets"
- **Cancel**: Kill running extraction at any time
- **Open output folder**: Open results in system file manager
- **Single GUID extraction**: Extract one specific asset by its GUID number
- **Live console**: Per-asset progress with color-coded log lines

**Template Management (CLI)**
- `--update-templates`: Scrape game's `templates.xml` and update config
- `--compare-templates`: Diff config templates against game templates
- `--auto-templates`: Extract all templates dynamically at runtime
- `-u <file>` / `-x <file>`: Custom template list or fixlist files

**Config-Driven** — no recompile needed to adjust behavior:
| Config Directory | Contains | Entries |
|---|---|---|
| `01_Templates/` | Template lists per game | 1,938 total |
| `02_Processing_Rules/` | Fixlist files per game | 1,907 total |
| `03_Regional_Ingredients/` | Anno 1800 regional ingredient mapping | 8 ingredients across 3 regions |
| `04_Comment_Whitelist/` | Property names eligible for GUID comments | 1,914 total |
| `05_Console_Messages/` | Localized backend output strings | 61 keys per language |

### Technical

- **.NET 10** + **Avalonia UI 11.3.16** — cross-platform desktop framework
- **CommunityToolkit.Mvvm 8.4.2** — MVVM source generators
- **Newtonsoft.Json 13.0.4**, **SkiaSharp 2.88.9**
- **C# 14**, nullable enabled, warnings treated as errors
- **4 projects**: RDAExplorer (library), AssetSplitter (backend), RDAExtract (standalone CLI), AssetSplitterUI (desktop app)
- Windows x64 pre-built zips; Linux/macOS supported via build-from-source

### Distribution
- `Asset-Splitter-UI-v1.0.0-win-x64-self-contained.zip` — includes .NET 10 runtime
- `Asset-Splitter-UI-v1.0.0-win-x64-framework.zip` — requires .NET 10 installed
- Unzip and run `AssetSplitterUI.exe`

---

*Based on Pogobuckel's AssetSplit concept and ModTool library. See [CREDITS.md](CREDITS.md).*
