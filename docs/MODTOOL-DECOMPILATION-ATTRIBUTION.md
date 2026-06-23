# ModTool / Decompilation Attribution

This document records which parts of this codebase derive from decompiled or adapted source code, who the original authors are, and what was and was not carried over.

---

## Origin

The core asset-splitting logic derives from **Holger Eilts (Pogobuckel)**'s original **AssetSplit** / **BaseAssetFixer** tools, which were part of the [Anno 1800 ModTool](https://annomodtool.wixsite.com/main) collection (version 5.55). The originals were distributed as compiled .NET Framework binaries. The logic here was reconstructed via decompilation using **ILSpy 10.1.0.8386** ([ILSpy releases](https://github.com/icsharpcode/ILSpy/releases)) and then significantly rewritten and extended.

Full attribution: [CREDITS.md](../CREDITS.md)

---

## What was in the decompiled ModTool package

The decompiled directory contained **seven distinct executables / libraries**:

| Component | Namespace | Framework | Lines | Purpose |
|-----------|-----------|-----------|-------|---------|
| **AssetSplit** (BaseAssetFixer) | `BaseAssetFixer` | net472 | ~936 | **Primary asset splitter** — reads `assets.xml`, filters by template list, writes per-asset `<ModOps>` XML, merges templates, resolves `BaseAssetGUID` inheritance, inserts GUID comments, organizes by template folder. This is the **direct origin** of our `src/AssetSplitter/` pipeline. |
| **AssetFix** | `AssetFix` | net472 | ~833 | Standalone asset fixer — merges a partial asset against its base, fills in template defaults, injects ingredient region IDs. Also contains spline editing and world-map XML manipulation. Partially related to our ingredient region logic. |
| **WatchDog** | `WatchDog` | — | ~777 | ModTool GUI application — XML asset collection management, mod composition, `FindByTarget`, file watching. Not the source of our asset pipeline. |
| **RDArchiver** | `RDArchiver` | net461 | ~119 | RDA archive extraction CLI — sequentially processes `data*.rda` files, pattern-based file filtering. Direct origin of our `src/RDAExtract/App.cs`. |
| **BeautifyXML** | `BeautifyXML` | — | ~167 | Standalone XML formatter and GUID commenter. Not used. |
| **CreateCache** | `CreateCache` | — | ~1,069 | Mod collection cache builder — scans mod folders, builds language/icon/production-chain caches, writes `.cache` files. Not used. |
| **ModTool** (library) | `ModTool` / `ModTool.Misc` / `ModTool.ZLib` | — | ~10 files | RDA format library: `RDAFile`, `RDAReader`, `RDAFolder`, `BlockInfo`, `DirEntry`, `FileHeader`, `BinaryExtension`, `ZLib`, etc. This is the **direct origin** of our `src/RDAExplorer/` library. |

---

## What was adapted into this project

### 1. Core asset-splitting pipeline (`src/AssetSplitter/`)

**Origin:** `BaseAssetFixer/Program.cs` (namespace `BaseAssetFixer`) — the `AssetSplit 1.0, 2022 by Pogobuckel` tool

The `BaseAssetFixer` contained the original concepts for:
- Filtering `assets.xml` by template name list (`templates_used`, 130+ entries)
- Resolving `BaseAssetGUID` inheritance chains (two-pass: extract to `BaseAssetGUID/` subfolder, then fix-up pass)
- Building GUID → translated name dictionaries from `texts_<lang>.xml`
- Writing per-asset output XML wrapped in `<ModOps><ModOp GUID='...' Type='Replace' Path='/'>` structure
- Template merging: expanding partial assets against `templates.xml` default properties
- Inserting translated GUID comment nodes inline (`-c` flag / `formatXML`)
- Organizing output into template-named subfolders (`-t` flag / `xmlSaveToFolders`)
- Hardcoded `fixlist` (67 templates) controlling which assets get full template merging
- Ingredient region ID injection (`AssociatedRegions` → Africa vs. default ingredient GUIDs)

Additionally, `AssetFix/AssetFix.cs` contributed:
- The `FixFile` method pattern (XML node path walking + replacement) — adapted into our dependency resolution
- The `fixlist` (same 67 template names) — these became the seed for `config/01_Templates/` but were expanded and made config-driven

All of this logic was **rewritten from scratch** across the `src/AssetSplitter/` project files. The concepts are Pogobuckel's; the implementation is new:
- Targeting .NET 10 (originals target net461 / net472)
- Config-driven via external files in `config/` (were hardcoded arrays inside the binary)
- Anno 117 support added (originals are Anno 1800 only)
- Parallel processing with `Parallel.ForEach` (originals are sequential)
- External console message localization in 11 languages (originals hardcode English strings)
- GUI integration via the UI runner / coordinator process-launch flow

### 2. RDA extraction CLI (`src/RDAExtract/App.cs`)

**Origin:** `RDArchiver/App.cs` (namespace `RDArchiver`, class `App`)

The CLI structure is directly derived. Both share:
- Help banner text ("`RDAExtract is part of the RDAExplorer Collection (c) 2022 Pogobuckel`" preserved)
- Command syntax: `RDAExtract <RDA_path> <match> <out> [-n] [-d]`
- Sequential processing of `data0.rda` … `dataN.rda` in **descending** index order
- Pattern-based `match` string with `+` (AND) and `;` (OR) semantics passed to `RDAFileExtension.ExtractAll`
- `-n` flag (bare output, no paths) and `-d` flag (directory listing only)

The rewrite modernizes to .NET 10, adds support for single `.rda` file input, uses named record types, and integrates `RDAExplorer` as a library reference rather than depending on `ModTool.dll`.

### 3. RDA archive library (`src/RDAExplorer/`)

**Origin:** `ModTool/` library (namespace `ModTool`) — decompiled from `ModTool.dll` included in ModTool 5.55

The file-by-file correspondence is direct:

| Our file | ModTool original | Changes |
|----------|-----------------|---------|
| `RDAExplorer/BlockInfo.cs` | `ModTool/BlockInfo.cs` | Converted `struct` → `record struct`; renamed fields PascalCase; added XML docs |
| `RDAExplorer/DirEntry.cs` | `ModTool/DirEntry.cs` | Same structure; renamed fields |
| `RDAExplorer/FileHeader.cs` | `ModTool/FileHeader.cs` | Same version enum; renamed `Version_2_0` → `Version20` etc. |
| `RDAExplorer/RDAFile.cs` | `ModTool/RDAFile.cs` | Converted to auto-properties; removed `OverwrittenFilePath`/`overwrite` mutable fields; added XML docs |
| `RDAExplorer/RDAFolder.cs` | `ModTool/RDAFolder.cs` | Same structure |
| `RDAExplorer/RDAMemoryResidentHelper.cs` | `ModTool/RDAMemoryResidentHelper.cs` | Same purpose |
| `RDAExplorer/RDAReader.cs` | `ModTool/RDAReader.cs` | Same block-reading and file-extraction logic |
| `RDAExplorer/RDASkippedDataSection.cs` | `ModTool/RDASkippedDataSection.cs` | Same |
| `RDAExplorer/Misc/BinaryExtension.cs` | `ModTool.Misc/BinaryExtension.cs` | Same LCG XOR cipher and version seeds (666666 / 1908874353); rewritten with `BinaryPrimitives` instead of `BinaryReader`/`BinaryWriter`; converted to static class |
| `RDAExplorer/Misc/DateTimeExtension.cs` | `ModTool.Misc/DateTimeExtension.cs` | Same |
| `RDAExplorer/ZLib/ZLib.cs` | `ModTool.ZLib/ZLib.cs` | Same interface; replaced `[DllImport("zlib.DLL")]` P/Invoke with managed `System.IO.Compression.ZLibStream` |

Files added by this project with no ModTool equivalent: `RDAFileExtension.cs`, `RdaArchiveDiagnostics.cs`, `RdaExtractStatistics.cs`, `RdaFileExtractor.cs`, `ArchivePathSanitizer.cs`, `RDAValidation.cs`.

---

## What was NOT carried over

### BaseAssetFixer / AssetFix features not included

| Original feature | Reason not included |
|-----------------|---------------------|
| `fixlist` (67-template hardcoded array) | Replaced by config-driven `config/01_Templates/` external files |
| `templates_used` (130-entry hardcoded array) | Replaced by `Anno1800_Templates.txt` / `Anno117_Templates.txt` |
| Two-pass `BaseAssetGUID/` folder resolution | Replaced by single-pass `DependencyResolutionOrchestrator` |
| Ingredient region ID injection (hardcoded GUIDs) | Replaced by `config/03_Regional_Ingredients/regional_ingredients.json` |
| `-m` / `modtool_modtool` mode (`collection.xml` assembly) | Not needed — pipeline does not produce a collection |
| `fixer.txt` progress file written to disk | Replaced by in-process progress reporting to the UI |
| `xmlSaveToFolders` fixed subfolder hierarchy | Preserved in `OutputDirectoryManager`, made config-driven |

### WatchDog features not included

| WatchDog feature | Reason not included |
|-----------------|---------------------|
| `ReplaceSplines` / `ReadSplines` / `CountSplines` | Spline editing, out of scope |
| `fixWorldMap` / `deleteWorldMap` | Session map XML tool, out of scope |
| `compose` command — composing mod collections | Separate concern |
| `show_all` / `add_asset` / `create_collection` | Collection management, not extraction |
| `FindByTarget` — querying by target GUID | Query tool, not needed in batch pipeline |
| `FileSystemWatcher` — real-time update detection | Not needed in a batch tool |
| Hardcoded `g_allSkins` / `g_allProducts` / etc. arrays | Replaced by config-driven template files |

### CreateCache / BeautifyXML — not used at all

`CreateCache` builds production-chain and icon caches for the ModTool GUI editor. `BeautifyXML` is a standalone formatter. Neither has an equivalent in this project.

### ModTool.ZLib P/Invoke — replaced

The original used `[DllImport("zlib.DLL")]` requiring a native `zlib.dll` alongside the binary. Our `ZLib.cs` uses the managed `System.IO.Compression.ZLibStream` (built into .NET 10), removing the native dependency entirely.

---

## Version delta

| Aspect | Original ModTool 5.55 | This repo (v1.0.0) |
|--------|----------------------|---------------------|
| Framework | .NET Framework 4.6.1 / 4.7.2 | .NET 10 |
| Anno support | Anno 1800 only | Anno 1800 + Anno 117 |
| Templates / fixlists | Hardcoded static arrays (67–130 entries) | External `.txt` config files |
| Console messages | Hardcoded English strings | External JSON, 11 languages |
| UI | Console-only | Avalonia cross-platform GUI + console backend |
| Processing | Sequential `foreach` | Parallel (`Parallel.ForEach`, CPU-1 threads) |
| Dependency resolution | Two-pass (extract → `BaseAssetGUID/` → fix pass) | Single-pass `DependencyResolutionOrchestrator` |
| ZLib | Native `zlib.DLL` P/Invoke | Managed `System.IO.Compression.ZLibStream` |
| Ingredient regions | Hardcoded GUID arrays | `config/03_Regional_Ingredients/regional_ingredients.json` |
| JSON | None | `System.Text.Json` (built-in to .NET 10) |

---

## Decompilation tools used

- **ILSpy 10.1.0.8386** — [github.com/icsharpcode/ILSpy](https://github.com/icsharpcode/ILSpy/releases) — used to reconstruct C# source from the compiled ModTool 5.55 binaries

---

*This project is not part of the ModTool package and is not affiliated with the anno-mods organization. See [CREDITS.md](../CREDITS.md) for full attribution and [docs/LEGAL.md](LEGAL.md) for compliance notes.*