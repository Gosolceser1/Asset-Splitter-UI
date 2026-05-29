# AssetSplitter (AssetProcessor)

Console backend for Asset Splitter UI. Builds to **`AssetProcessor.exe`**.

## Pipeline

Orchestrated by **`PipelineOrchestrator`** using shared **`PipelineContext`**:

1. RDA extraction (via `RDAExplorer`)
2. Config load (`config/` — templates, fixlist, whitelists, `app_settings.json`)
3. Per-asset extraction from `assets.xml`
4. Template property merge (`TemplateMergeOrchestrator`, fixlist only)
5. `BaseAssetGUID` dependency resolution
6. Final formatting (`FormattingService`)
7. Optional asset mod packages (`AssetModPackageExporter` + **`ModReadmeWriter`**)

## Key types

| Category | Types |
|----------|--------|
| Entry | `Program` |
| Orchestration | `PipelineOrchestrator`, `PipelineContext`, `PipelineLogger` |
| Templates / XML | `TemplateLoader`, `TemplateExtractor`, `TemplateMergeService`, `AssetExtractor` |
| Formatting | `FormattingService`, `FinalFormattingOrchestrator`, `GuidFileIndex` |
| Mod packages | `AssetModPackageExporter`, `ModReadmeWriter` |
| Issues | `Issues/PipelineIssueTracker`, `PipelineIssueReporter` |
| Config / i18n | `AssetProcessorConfiguration`, `ConsoleMessages` |
| Utilities | `GameTypeDetector`, `CommentWhitelistLoader`, `AssetTextSanitizer`, `XmlLeafPathBuilder` |

Config DTOs: `*Config.cs`, `*Settings.cs`.

## Config and messages

- Game data rules: repo **`config/`** — see [config/README.md](../../config/README.md).
- Console + generated mod README strings: **`config/05_Console_Messages/console_*.json`** (`readme*` keys used by `ModReadmeWriter`).

Path resolution: `AssetProcessorConfiguration.GetConfigPath()`, `TemplateLoader.ResolveConfigPath()`.

## Run locally

```bash
# From repo root, after build
dotnet run --project src/AssetSplitter/AssetSplitter.csproj -- <gamePath> <annoAssetsPath> <language> [options]

# Verbose developer log
... -d
```

The GUI builds arguments via `AssetSplitterUI` (`AssetProcessorRunConfig`, `GuiProcessRunner`).

## Related docs

- [Issues/README.md](Issues/README.md) — structured warnings/errors in debug mode
- [../README.md](../README.md) — solution overview
- [../../docs/UNDERSTANDING-THE-CODEBASE.md](../../docs/UNDERSTANDING-THE-CODEBASE.md) — deeper walkthrough
