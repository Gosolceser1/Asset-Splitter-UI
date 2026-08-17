# Architecture

> **Status:** current  
> **Updated:** 2026-08-16  
> **Owner:** maintainers

A walkthrough of Asset Splitter UI for developers and contributors.

---

## Solution layout

```
AssetSplitter.sln
├── src/AssetSplitterUI/        — Avalonia desktop UI (WinExe, net10.0)
├── src/AssetSplitter/          — Console backend → AssetProcessor.exe (net10.0)
├── src/RDAExplorer/            — RDA archive read/decompress library
├── src/RDAExtract/             — Standalone CLI for listing/extracting RDA files
├── tests/AssetSplitter.Tests/  — Backend / RDA xUnit tests
└── tests/AssetSplitterUI.Tests/ — UI service xUnit tests
```

---

## The projects

### AssetSplitterUI
The desktop front-end. Built with [Avalonia UI](https://avaloniaui.net/) so it targets Windows, macOS, and Linux from a single codebase.

- **`MainWindowViewModel`** holds the main UI state and commands: game path, output path, language pickers, processing options (comments, fix deps, ModOps wrap, etc.), progress, and the log. Filesystem probing, settings persistence, console parsing, and backend-output localization live in services.
- When the user runs a phase, the ViewModel spawns `AssetProcessor.exe` as a child process and reads its stdout line-by-line.
- **Localization of backend output:**
  1. Prefer structured `[I18N] {"key":"consoleMessages.…","args":[…]}` markers (`BackendI18nLine` / `PipelineLogger.WriteLocalized`) when stdout is redirected.
  2. Fall back to English string matching in `ConsoleOutputLocalizer` (exact keys, parameterized patterns, progress label swaps).
- Progress strings have the format `[ 12.5%] [ 100/792] - Operation...`. `ConsoleProgressLineParser` parses the percentage from each output line.
- Log lines are batched through a `ConcurrentQueue` and a timer. `MainWindowLogStore` owns the observable log collection, localized log descriptors, snapshots, and the line cap (4000 in normal mode, 12000 in developer mode).
- There are **two separate language pickers**:
  - `SelectedUILanguage` — the app interface language (loads from `Localization/Languages/Strings.xx.json`)
  - `SelectedLanguage` — the game language used to resolve GUID display names in output XML
- **Theme** (Light / Dark / Auto) is persisted in settings and applied via `ApplyTheme()`.
- Settings (paths, options, theme, UI language, recent paths) are saved by `AppSettingsStore` to `%AppData%\AssetSplitter\settings.json`.
- `RecentGamePaths` and `RecentOutputPaths` (up to 10 entries each) are maintained and shown as dropdowns in their respective path fields.
- `ChildProcessTracker` ensures the backend process is killed if the UI closes.

#### Two-phase UI model
The processing run is split into two distinct phases triggered by the same **Extract / Process Assets** button:

1. **Phase 1 — RDA extraction**: The backend is called with `language="none"`. It extracts game archives into `source_xml_anno1800/` or `source_xml_anno117/`, then exits. The ViewModel sets `Phase1Complete = true` and a banner prompts the user to select a language and click again.
2. **Phase 2 — Asset processing**: A full pipeline run (config → extract → merge → deps → format → optional mod packages). Available only after Phase 1 (or when source XML files are already present from a prior run).

If source XML files already exist on disk, the UI skips the Phase 1 button state and goes straight to Phase 2.

Key files: `MainWindowViewModel*.cs`, `GuiProcessRunner.cs`, `ConsoleOutputCoordinator.cs`, `ConsoleOutputLocalizer*.cs`, `ConsoleProgressLineParser.cs`, `MainWindowLogStore.cs`, `ExtractionRunResultAppender.cs`, `PlatformServices.cs`, `AnnoInstallationDetector.cs`, `AppSettingsStore.cs`, `ExtractedAssetSourceLocator.cs`

### AssetSplitter (→ AssetProcessor.exe)
Console backend. `PipelineOrchestrator.Run` is a thin coordinator; phase work lives in focused helpers.

**Phase helpers:**
| Step | Type | Responsibility |
|------|------|----------------|
| Bootstrap | `PipelineBootstrap` | Context, paths, game detection |
| 1 | `Phase1SourceAcquisition` + `RdaArchiveExtractor*` | Decompress RDA → `source_xml_*` |
| 2 | `Phase2ConfigLoading` | Templates, dictionaries, property scan; optional split-templates |
| 3 | `Phase3AssetExtraction` + `AssetExtractor` | Per-asset ModOp/XML output |
| 4 | GUID index | `GuidFileIndex` (when `-c` or `-f`) |
| 5 | `TemplateMergeOrchestrator` | Fixlist template inheritance |
| 6 | `DependencyResolutionOrchestrator` | `<BaseAssetGUID>` chains (`-f`) |
| 7 | `FinalFormattingOrchestrator` / `FormattingService*` | Comments, regional costs, folders |
| 8 | `AssetModPackageExporter*` | Optional Mod Loader packages + README |

**Shared state:** `PipelineContext`. Feature gates: `PipelineFeatureGates` / `ProcessingRunPolicy`.

**Output run cache (`OutputRunCache`, schema 6):** After Phase 1, identical full-processing runs can **Complete** (skip), **Repair** (surgical GUID gaps / templates resplit), **DiffRepair** (game update or `source_xml` extractable-hash changes), or **Full** (options/schema/output_xml extras / force). Tradeoffs:

| Change | Mode | Notes |
|--------|------|--------|
| Edit one mod package / flat XML | Repair | Rebuild that GUID only (`RepairOnlyGuids`); preserve other outputs |
| Edit `output_templates_*` only | Repair + `ForceResplitTemplates` | Empty `RepairOnlyGuids` → skip Phase 3 extract; re-split templates |
| Edit `source_xml_*` (keep files) | DiffRepair or Complete | Phase 1 keeps on-disk source when required files are well-formed; diffs extractable hashes — no full output wipe |
| Inventory missing `sourceHash` | Complete / DiffRepair | Backfill when source tree + outputs intact; if source digest moved or presence fails, DiffRepair blank-hash GUIDs — not Full wipe |
| Corrupt required `source_xml` file | Phase 1 RDA | Empty / non-`<` / XML load failure → re-extract; do not keep bad source |
| Extra file under `output_xml` / mods root | Full | Cannot map arbitrary extras to GUID repair |
| Schema / options / force | Full | Intentional wipe |

Repair of one GUID still runs Phase 2 (language/dictionaries) and may rebuild GuidIndex over the full tree when `-c`/`-f` — needed for comments/deps; template re-annotate runs only when templates were resplit.

**GUI stdout contract:** Interactive CLI prints English via `ConsoleMessages`. When stdout is redirected, milestones should emit `[I18N]` (`BackendI18nLine.Write` / `PipelineLogger.WriteLocalized`) so the UI resolves `consoleMessages.*` without English matching. Backend language for GUI runs is forced to English (`-l:english`); UI language is independent.

Key files: `PipelineOrchestrator.cs`, `PipelineBootstrap.cs`, `Phase1SourceAcquisition.cs`, `Phase2ConfigLoading.cs`, `Phase3AssetExtraction.cs`, `PipelineRunSupport.cs`, `BackendI18nLine.cs`, `PipelineLogger.cs`, `PipelineContext.cs`, `RdaArchiveExtractor*.cs`, `OutputDirectoryManager*.cs`, `ConsoleMessages*.cs`, `FormattingService.cs`, `AssetModPackageExporter*.cs`, `ModReadmeWriter*.cs`

### RDAExplorer
Library for parsing Anno `.rda` (Resource Data Archive) files. Supports V2.0 and V2.2; handles encryption (seed `1908874353` for V2.2) and zlib decompression.

- **`RDAReader`** — parse RDA headers, decrypt blocks, decompress
- **`RDAFolder` / `RDAFile`** — folder tree and file list abstraction
- **`RDAFileExtension`** — entry points: `ExtractAll()`, `ListAll()`; each call creates its own reader via `using`

### RDAExtract
Thin CLI wrapper around RDAExplorer. Used for standalone RDA inspection; AssetSplitter Phase 1 uses `RdaArchiveExtractor` + RDAExplorer directly.

---

## Data flow

```
User selects game path + options in UI
        │
        ▼
[Phase 1 click]
AssetSplitterUI spawns AssetProcessor.exe  (language=none)
        │
        ▼
Phase 1: RdaArchiveExtractor reads data*.rda / config.rda → source_xml_*
        │  (stdout may include [I18N] milestones + [PHASE1_ONLY] marker)
        ▼
UI sets Phase1Complete = true, shows banner ("select language, click again")
        │
        ▼
[Phase 2 click — user selects language and options]
AssetSplitterUI spawns AssetProcessor.exe  (full pipeline, -l:english)
        │
        ▼
Phase 2–8: config → extract → merge → deps → format → optional mods
        │
        ▼
output_xml_* / optional *_mods — ModOp XML (+ packages)
```

### UI → CLI flag mapping (backend names)
| UI concept | Backend option | CLI switch |
|------------|----------------|------------|
| GUID comments | `AddComments` | `-c` |
| Fix dependencies | `FixDependencies` | `-f` |
| Template folders | `CreateTemplateFolders` | `-t` |
| ModOps wrap | `ModOpsWrap` (off → `--no-modops-wrap`) | |
| Default properties | `NoDefaultProperties` | `--no-default-properties` |
| Split templates | `SplitTemplates` | `--split-templates` |
| Create asset mods | `CreateAssetMods` | `--create-asset-mods` |
| Single GUID | `SingleAssetGuid` | `-g` |
| Debug | `DebugMode` | `-d` |

---

## Config path resolution

Config files must be found in both development builds (running from `bin/Debug/`) and installed/published builds (running from the publish folder). Prefer `ConfigPathResolver` / `TemplateLoader.ResolveConfigPath` / `AssetProcessorConfiguration.GetConfigPath`.

- **Templates and fixlists** — `TemplateLoader.ResolveConfigPath(subfolder, filename)`
- **app_settings.json, regional_ingredients.json** — `AssetProcessorConfiguration.GetConfigPath(filename)`
- **Console messages** — `ConsoleMessages` loads `config/05_Console_Messages/console_{language}.json`; embedded English defaults live in `ConsoleMessages.Defaults.*.cs`

---

## Game detection

`PlatformServices.DetectGameInstallationsAsync()` delegates to `AnnoInstallationDetector`, which probes:

1. **Windows Registry** — Ubisoft Launcher / Steam install keys
2. **Common paths** — default Steam directories on Windows, Linux, macOS
3. **Steam library folders** — `libraryfolders.vdf`

Each candidate is classified as Anno 1800 or Anno 117. The backend also re-checks via `GameTypeDetector` / `assets.xml` content.

---

## Performance patterns

- **Template caches** — fixlist templates pre-loaded via `InitializeTemplateCaches`
- **Parallel** — `Parallel.ForEach` with `MaxDegreeOfParallelism = Max(1, Environment.ProcessorCount - 1)` (shared XML mutation must stay locked — see `TemplateMergeOrchestrator`)
- **Parent asset cache** — during BaseAssetGUID resolution

---

## Code origins and caveats

Some code originated from decompilation of Pogobuckel's original AssetSplit tool (see [CREDITS.md](../../CREDITS.md) and [attribution.md](../legal/attribution.md)):

- Prefer `#nullable enable` and fix or suppress incomplete annotations when touching code
- Rename terse locals (`str1`, `flag1`) when editing surrounding code
- Do not crash on missing config files — log a warning and fall back to built-in defaults

---

## Adding a feature: checklist

1. **New config option** — `app_settings.json` + `AppSettingsConfig.cs`; document in `config/README.md`
2. **New processing phase** — add a phase helper; keep `PipelineOrchestrator` thin; emit `[I18N]` for GUI milestones
3. **New UI option** — bind in `MainWindow.axaml`; ViewModel + subprocess args in `GuiProcessRunner`
4. **New language** — `Strings.xx.json` under `Localization/Languages/` and optionally `console_xx.json`

---

## Key files reference

| File | What it does |
|------|-------------|
| `PipelineOrchestrator.cs` | Thin run loop / exit codes |
| `PipelineBootstrap.cs` / `Phase*Source*.cs` | Phase helpers |
| `BackendI18nLine.cs` / `PipelineLogger.cs` | GUI `[I18N]` + console logging |
| `ConsoleMessages*.cs` | Embedded + file-backed console strings |
| `RdaArchiveExtractor*.cs` | Phase 1 archive extraction |
| `OutputDirectoryManager*.cs` | Output layout / staging / cleanup |
| `FormattingService.cs` + annotator/cost helpers | Final XML formatting |
| `AssetModPackageExporter*.cs` / `ModReadmeWriter*.cs` | Mod packages + README |
| `ConsoleOutputLocalizer*.cs` | UI localization of backend stdout |
| `ConsoleProgressLineParser.cs` | Progress percentage / count parsing |
| `GuiProcessRunner.cs` | Spawns AssetProcessor and streams stdout |
| `MainWindowViewModel*.cs` | UI state and commands |
| `AnnoInstallationDetector.cs` | Game install detection |
| `RDAReader.cs` / `RDAFileExtension.cs` | RDA parse / extract API |
