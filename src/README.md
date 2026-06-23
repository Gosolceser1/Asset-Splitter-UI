# Source code overview

Solution: **`AssetSplitter.sln`** at the repo root.

## Projects

| Project | Output | Role |
|---------|--------|------|
| **AssetSplitterUI** | `AssetSplitterUI.exe` | Avalonia desktop UI — paths, options, progress, log, settings |
| **AssetSplitter** | `AssetProcessor.exe` | Console pipeline (RDA → extract → merge → deps → format → optional mod packages) |
| **RDAExplorer** | `RDAExplorer.dll` | RDA archive reader library |
| **RDAExtract** | `RDAExtract.exe` | Standalone RDA list/extract CLI (testing / manual use) |

**Build**

```bash
dotnet build AssetSplitter.sln -c Debug
dotnet run --project src/AssetSplitterUI/AssetSplitterUI.csproj -c Debug
```

**Scripts:** `scripts/build.ps1 -Fast` (daily), `scripts/build.ps1` (full clean + publish). Shared MSBuild: `Directory.Build.props` (warnings as errors).

---

## Pipeline (AssetProcessor)

| Phase | What happens |
|-------|----------------|
| 1 | Extract language/source XML from `.rda` (`RDAExplorer`) |
| 2 | Load config from `config/` (templates, fixlist, settings) |
| 3 | Split `assets.xml` into per-asset XML / ModOps |
| 4 | Merge template properties (fixlist templates) |
| 5 | Resolve `BaseAssetGUID` dependencies |
| 6 | Format (comments, template folders, cleanup) |
| 7 | Optional: one Mod Loader folder per asset (`--create-asset-mods`) |

Config path resolution: `AssetProcessorConfiguration.GetConfigPath()` / `TemplateLoader.ResolveConfigPath()` (next to the exe, then dev repo paths).

**Developer mode:** pass **`-d`** to `AssetProcessor.exe` for verbose backend logging. The UI exposes this as **Debug mode**.

---

## UI integration

- **`ExtractionCoordinator`** / **`AssetProcessorRunner`** spawn `AssetProcessor.exe` via **`GuiProcessRunner`**.
- Stdout → **`ConsoleOutputLocalizer`**, **`ConsoleLineClassifier`**, **`ConsoleProgressParser`** → progress bar and colored log.
- **`MainWindowViewModel`** (partials under `ViewModels/`) owns state; **`MainWindowLogStore`** holds log lines.

See [AssetSplitterUI/README.md](AssetSplitterUI/README.md) and [AssetSplitterUI/Services/README.md](AssetSplitterUI/Services/README.md).

---

## Notable backend modules

| Area | Types / files |
|------|----------------|
| Orchestration | `PipelineOrchestrator`, `PipelineContext`, `PipelineLogger` |
| Issues (debug) | `Issues/` — `PipelineIssueTracker`, `PipelineIssueReporter` → `AnnoAssets/logs/issues_*.json` |
| Mod export | `AssetModPackageExporter`, **`ModReadmeWriter`** (`MODDING-GUIDE.md` + short per-mod README) |
| GUID index | `GuidFileIndex` |
| RDA | `RDAReader`, `RDAFileExtension` — see [RDAExplorer/README.md](RDAExplorer/README.md) |

---

## Per-project READMEs

| Path | Topic |
|------|--------|
| [AssetSplitter/README.md](AssetSplitter/README.md) | Backend entry and types |
| [AssetSplitter/Issues/README.md](AssetSplitter/Issues/README.md) | Structured issue tracking |
| [AssetSplitterUI/README.md](AssetSplitterUI/README.md) | UI structure |
| [RDAExplorer/README.md](RDAExplorer/README.md) | RDA format and API |
| [RDAExtract/README.md](RDAExtract/README.md) | Standalone RDA CLI |

---

## Credits

Core extraction and RDA logic build on **Holger Eilts (Pogobuckel)**. See [CREDITS.md](../CREDITS.md).
