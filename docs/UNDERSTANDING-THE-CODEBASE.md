# Understanding the Codebase

A walkthrough of Asset Splitter UI for developers and contributors.

---

## Solution layout

```
AssetSplitter.sln
├── src/AssetSplitterUI/        — Avalonia desktop UI (WinExe, net10.0)
├── src/AssetSplitter/          — Console backend → AssetProcessor.exe (net10.0)
├── src/RDAExplorer/            — RDA archive read/decompress library
└── src/RDAExtract/             — Standalone CLI for listing/extracting RDA files
```

---

## The four projects

### AssetSplitterUI
The desktop front-end. Built with [Avalonia UI](https://avaloniaui.net/) so it targets Windows, macOS, and Linux from a single codebase.

- **`MainWindowViewModel`** holds the main UI state and commands: game path, output path, language pickers, processing options (comments, fix deps, ModOps wrap, etc.), progress, and the log. Filesystem probing, settings persistence, console parsing, and backend-output localization live in services.
- When the user runs a phase, the ViewModel spawns `AssetProcessor.exe` as a child process and reads its stdout line-by-line. **The backend always emits English on stdout**; the ViewModel translates progress labels (e.g. `Formatting...`, `Merging...`) into the selected UI language before appending them to the log.
- Progress strings have the format `[ 12.5%] [ 100/792] - Operation...`. `ConsoleProgressParser` parses the percentage and the ViewModel dispatches updates only when it changes by at least 0.1% to avoid flooding the UI thread.
- Log lines are batched through a `ConcurrentQueue` and a timer. `MainWindowLogStore` owns the observable log collection, localized log descriptors, snapshots, and the 2500-line cap.
- There are **two separate language pickers**:
  - `SelectedUILanguage` — the app interface language (loads from `Localization/Languages/Strings.xx.json`)
  - `SelectedLanguage` — the game language used to resolve GUID display names in output XML
- **Theme** (Light / Dark / Auto) is persisted in settings and applied via `ApplyTheme()`.
- Settings (paths, options, theme, UI language, recent paths) are saved by `AppSettingsStore` to `%AppData%\AssetSplitter\settings.json`.
- `RecentGamePaths` (up to 10 entries) is maintained and shown as a dropdown in the game path field.
- `ChildProcessTracker` ensures the backend process is killed if the UI closes.

#### Two-phase UI model
The processing run is split into two distinct phases triggered by the same **Extract / Process Assets** button:

1. **Phase 1 — RDA extraction**: The backend is called with `language="none"`. It extracts game archives into the `source_xml` working folder, then exits. The ViewModel sets `Phase1Complete = true` and a banner prompts the user to select a language and click again.
2. **Phase 2 — Asset processing**: A full 6-phase pipeline run. Available only after Phase 1 (or when `source_xml` files are already present from a prior run).

If `source_xml` files already exist on disk, the UI skips the Phase 1 button state and goes straight to Phase 2.

Key files: `src/AssetSplitterUI/ViewModels/MainWindowViewModel.cs`, `src/AssetSplitterUI/ViewModels/MainWindowLogStore.cs`, `src/AssetSplitterUI/ViewModels/BusyIndicatorAnimator.cs`, `src/AssetSplitterUI/ViewModels/LocalizedTextState.cs`, `src/AssetSplitterUI/Views/MainWindow.axaml`, `src/AssetSplitterUI/Services/AssetProcessorRunner.cs`, `src/AssetSplitterUI/Services/ExtractionRunResultAppender.cs`, `src/AssetSplitterUI/Services/PlatformServices.cs`, `src/AssetSplitterUI/Services/AnnoInstallationDetector.cs`, `src/AssetSplitterUI/Services/ApplicationThemeService.cs`, `src/AssetSplitterUI/Services/AppSettingsStore.cs`, `src/AssetSplitterUI/Services/GameConsoleStateStore.cs`, `src/AssetSplitterUI/Services/ExtractedAssetSourceLocator.cs`, `src/AssetSplitterUI/Services/ConsoleProgressParser.cs`, `src/AssetSplitterUI/Services/ConsoleOutputLocalizer.cs`

### AssetSplitter (→ AssetProcessor.exe)
The 7-phase processing pipeline. Runs as a console app so the UI can stream its output.

**Phases:**
1. **RDA extraction** — decompress game RDA archives into a `source_xml` working folder
2. **Dictionary loading** — build GUID→name maps from `texts_*.xml`
3. **Asset extraction** — filter `assets.xml` by the template list, write per-asset ModOp XML
4. **Template merging** — for templates in the fixlist, inherit properties from `templates.xml`
5. **Dependency resolution** — resolve `<BaseAssetGUID>` chains (`-f` flag)
6. **Formatting** — GUID comments, regional ingredient replacement, folder organization
7. **Asset mod packaging** — generate standalone Mod Loader folders under `mods/` with `modinfo.json` and README

Key files: `src/AssetSplitter/AssetProcessor.cs` (small entry facade), `src/AssetSplitter/PipelineOrchestrator.cs`, `src/AssetSplitter/PipelineContext.cs`, `src/AssetSplitter/PipelineLogger.cs`, `src/AssetSplitter/AssetExtractor.cs`, `src/AssetSplitter/TemplateMergeService.cs`, `src/AssetSplitter/TemplateMergeOrchestrator.cs`, `src/AssetSplitter/DependencyResolutionOrchestrator.cs`, `src/AssetSplitter/RdaArchiveExtractor.cs`, `src/AssetSplitter/FormattingService.cs`, `src/AssetSplitter/TranslationRegistry.cs`, `src/AssetSplitter/AssetNameRegistry.cs`, `src/AssetSplitter/TranslationDictionaryLoader.cs`, `src/AssetSplitter/TemplateLoader.cs`, `src/AssetSplitter/TemplateExtractor.cs`, `src/AssetSplitter/ConsoleMessages.cs`, `src/AssetSplitter/AssetProcessorConfiguration.cs`, `src/AssetSplitter/AssetProcessorProgressReporter.cs`, `src/AssetSplitter/AssetProcessorConsole.cs`, `src/AssetSplitter/DependencyResolutionCache.cs`, `src/AssetSplitter/TemplatePropertyCache.cs`, `src/AssetSplitter/AssetProcessorFileSystem.cs`, `src/AssetSplitter/XmlLeafPathBuilder.cs`, `src/AssetSplitter/AssetXmlStructureNormalizer.cs`, `src/AssetSplitter/XmlNodeText.cs`, `src/AssetSplitter/AssetDocumentSaver.cs`, `src/AssetSplitter/AssetXmlPathEditor.cs`, `src/AssetSplitter/GameTypeDetector.cs`, `src/AssetSplitter/CommentWhitelistLoader.cs`, `src/AssetSplitter/AssetTextSanitizer.cs`

### RDAExplorer
Library for parsing Anno `.rda` (Resource Data Archive) files. Supports V2.0 and V2.2; handles encryption (seed `1908874353` for V2.2) and zlib decompression.

- **`RDAReader`** — parse RDA headers, decrypt blocks, decompress
- **`RDAFolder` / `RDAFile`** — folder tree and file list abstraction
- **`RDAFileExtension`** — entry points: `ExtractAll()`, `ListAll()`; each call creates its own reader via `using`

Key files: `src/RDAExplorer/RDAReader.cs`, `src/RDAExplorer/RDAFileExtension.cs`

### RDAExtract
Thin CLI wrapper around RDAExplorer. Used by AssetSplitter in Phase 1 to extract game files.

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
Phase 1: RDAExtract reads data*.rda / config.rda → source_xml/
        │
        ▼
UI sets Phase1Complete = true, shows banner ("select language, click again")
        │
        ▼
[Phase 2 click — user selects language and options]
AssetSplitterUI spawns AssetProcessor.exe  (full pipeline)
        │
        ▼
Phase 2: Load GUID→name dictionary from texts_*.xml
        │
        ▼
Phase 3: Filter assets.xml by template list → one XML file per asset
        │
        ▼
Phase 4: Template merge (fixlist templates only) — inherit from templates.xml
        │
        ▼
Phase 5: BaseAssetGUID resolution — fill in parent asset data (-f flag)
        │
        ▼
Phase 6: Format — comments, regional ingredients, folder layout
        │
        ▼
output_xml/ — one ModOp XML file per extracted asset
```

---

## Config path resolution

Config files must be found in both development builds (running from `bin/Debug/`) and installed/published builds (running from the publish folder). The pattern used throughout:

```csharp
// 1. Production path: exe directory / config / subfolder / file
string production = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", subfolder, file);
if (File.Exists(production)) return production;

// 2. Dev path: walk up from exe to repo root
string dev = Path.GetFullPath(Path.Combine(baseDir, "../../../..", "config", subfolder, file));
if (File.Exists(dev)) return dev;

return production; // for error messages
```

- **Templates and fixlists** — resolved via `TemplateLoader.ResolveConfigPath(subfolder, filename)`
- **app_settings.json, regional_ingredients.json** — resolved via `AssetProcessorConfiguration.GetConfigPath(filename)`
- **Console messages** — `ConsoleMessages` loads `config/05_Console_Messages/console_{language}.json` with its own fallback chain; falls back to English

---

## Game detection

`PlatformServices.DetectGameInstallationsAsync()` delegates to `AnnoInstallationDetector`, which runs three sequential probes to build a list of `GameInstallation` candidates:

1. **Windows Registry** — checks `HKLM\SOFTWARE\Ubisoft\Launcher\Installs` and `HKCU\SOFTWARE\Valve\Steam`
2. **Common paths** — hardcoded list of default Steam installation directories on Windows, Linux (`~/.steam/...`), and macOS (`~/Library/Application Support/Steam/...`)
3. **Steam library folders** — parses `libraryfolders.vdf` for non-default Steam libraries

Each candidate path is passed to `DetectGameType()` which looks for `Anno 117` / `Pax Romana` markers to distinguish the two supported games. The backend (AssetProcessor) also re-checks by scanning `assets.xml` content and defaults to Anno 1800 if ambiguous.

Once detected, the correct template list, fixlist, and comment whitelist are loaded automatically.

---

## Performance patterns

- **Template caches** — fixlist templates are pre-loaded into memory (~50 MB) at startup via `InitializeTemplateCaches` to avoid repeated XPath queries
- **Parallel** — `Parallel.ForEach` with `MaxDegreeOfParallelism = Max(1, Environment.ProcessorCount - 1)`
- **Parent asset cache** — parent assets are cached during BaseAssetGUID resolution to reduce repeated disk reads

---

## Code origins and caveats

Some code originated from decompilation of Pogobuckel's original AssetSplit tool (see [CREDITS.md](../CREDITS.md) and [MODTOOL-DECOMPILATION-ATTRIBUTION.md](MODTOOL-DECOMPILATION-ATTRIBUTION.md)):

- Nullable annotations may be incomplete — use `#nullable enable` and fix or suppress (`#pragma warning disable CS86xx`) where appropriate
- Some variable names are generic (`str1`, `flag1`) — rename when touching surrounding code
- **NU1701** warnings on WindowsAPICodePack/Shell are expected (targets older .NET)
- Do not crash on missing config files — log a warning and fall back to built-in defaults

---

## Adding a feature: checklist

1. **New config option** — add to `app_settings.json` schema and update `AppSettingsConfig.cs`; use `GetConfigPath()` for path resolution; document in `config/README.md`
2. **New processing phase** — add to `AssetProcessor.cs`; update `MainWindowViewModel` if new progress output is emitted
3. **New UI option** — bind in `MainWindow.axaml`; add to `MainWindowViewModel`; pass as a flag to the subprocess args
4. **New language** — add `Strings.xx.json` to `Localization/Languages/` and `console_xx.json` to `config/05_Console_Messages/`

---

## Key files reference

| File | What it does |
|------|-------------|
| `src/AssetSplitter/AssetProcessor.cs` | Console entry facade (5 lines, delegates to `PipelineOrchestrator.Run`) |
| `src/AssetSplitter/PipelineOrchestrator.cs` | Main 6-phase pipeline orchestration and exit-code flow |
| `src/AssetSplitter/PipelineContext.cs` | Per-run backend state shared across pipeline services |
| `src/AssetSplitter/PipelineLogger.cs` | Debug/always-show console logging wrapper |
| `src/AssetSplitter/TemplateLoader.cs` | Config path resolution, template/fixlist loading |
| `src/AssetSplitter/TemplateExtractor.cs` | Auto-updates template lists from game `templates.xml` |
| `src/AssetSplitter/ConsoleMessages.cs` | Localized console strings from `config/05_Console_Messages/` |
| `src/AssetSplitter/AssetProcessorConfiguration.cs` | Backend config path resolution and JSON loading helpers |
| `src/AssetSplitter/AssetProcessorProgressReporter.cs` | Writes fixer.txt progress and stdout progress lines |
| `src/AssetSplitter/AssetProcessorConsole.cs` | Colored console output and CLI banner formatting |
| `src/AssetSplitter/DependencyResolutionCache.cs` | Phase 5 parent-asset document cache and GUID-to-file index |
| `src/AssetSplitter/TemplatePropertyCache.cs` | Phase 4 template property cache and Anno 117-specific property filtering |
| `src/AssetSplitter/AssetProcessorFileSystem.cs` | Backend file listing, asset-file lookup, display-name parsing, and empty-directory checks |
| `src/AssetSplitter/XmlLeafPathBuilder.cs` | Recursive leaf XPath collection for template merge path creation |
| `src/AssetSplitter/AssetXmlStructureNormalizer.cs` | VectorElement cleanup and ModOp GUID attribute normalization |
| `src/AssetSplitter/XmlNodeText.cs` | Common XPath text lookup helper |
| `src/AssetSplitter/AssetDocumentSaver.cs` | Saves full ModOps documents or raw Asset-only output depending on processing options |
| `src/AssetSplitter/AssetXmlPathEditor.cs` | XML path creation and node upsert helpers used by template/dependency merging |
| `src/AssetSplitter/GameTypeDetector.cs` | Backend game-type detection helpers |
| `src/AssetSplitter/CommentWhitelistLoader.cs` | Loads game-specific XML comment whitelist rules |
| `src/AssetSplitter/AssetTextSanitizer.cs` | Sanitizes asset names for filenames and XML comments |
| `src/AssetSplitterUI/ViewModels/MainWindowViewModel.cs` | UI state and command coordination; delegates to `ExtractionCoordinator`, `SettingsCoordinator`, `ConsoleOutputCoordinator`, and other focused helpers |
| `src/AssetSplitterUI/ViewModels/ConsoleOutputCoordinator.cs` | Buffers backend log output and flushes to UI on a timer with localization resolution |
| `src/AssetSplitterUI/ViewModels/MainWindowLogStore.cs` | Observable console log collection, localized line refresh, line cap, and log snapshots |
| `src/AssetSplitterUI/ViewModels/BusyIndicatorAnimator.cs` | Spinner/ellipsis animation lifecycle for active extraction runs |
| `src/AssetSplitterUI/ViewModels/LocalizedTextState.cs` | Re-resolvable localized text state for status labels and raw backend status output |
| `src/AssetSplitterUI/Views/MainWindow.axaml` | Avalonia layout and bindings |
| `src/AssetSplitterUI/Services/PlatformServices.cs` | Game auto-detection (Registry, Steam, common paths) |
| `src/AssetSplitterUI/Services/AnnoInstallationDetector.cs` | Registry, Steam, Epic, and common-folder Anno installation detection |
| `src/AssetSplitterUI/Services/ApplicationThemeService.cs` | Applies Light/Dark/Auto Avalonia theme selection |
| `src/AssetSplitterUI/Services/AppSettingsStore.cs` | UI settings JSON persistence |
| `src/AssetSplitterUI/Services/GameConsoleStateStore.cs` | Per-game console/log snapshot storage |
| `src/AssetSplitterUI/Services/ExtractedAssetSourceLocator.cs` | Finds existing source XML folders and game languages |
| `src/AssetSplitterUI/Services/ConsoleProgressParser.cs` | Parses backend progress output |
| `src/AssetSplitterUI/Services/ConsoleOutputLocalizer.cs` | Filters/localizes backend console lines for the UI |
| `src/RDAExplorer/RDAReader.cs` | RDA parse, decrypt, decompress |
| `src/RDAExplorer/RDAFileExtension.cs` | `ExtractAll()` / `ListAll()` entry points |
