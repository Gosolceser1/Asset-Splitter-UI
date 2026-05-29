# ModTool / Decompilation Attribution

This document records which parts of this codebase derive from decompiled or adapted source code, who the original authors are, and what was and was not carried over.

---

## Origin

The core asset-splitting logic derives from **Holger Eilts (Pogobuckel)**'s original **AssetSplit** tool, which was part of the [Anno 1800 ModTool](https://annomodtool.wixsite.com/main) collection. The original tool was distributed as compiled .NET Framework 4.6.1 binaries. The logic here was reconstructed via decompilation using **ILSpy 10.0 Preview 2** ([ILSpy releases](https://github.com/icsharpcode/ILSpy/releases)) and then significantly rewritten and extended.

Full attribution: [CREDITS.md](../CREDITS.md)

---

## What was in the decompiled ModTool package

The decompiled directory contained three components:

| Component | File | Lines | Purpose |
|-----------|------|-------|---------|
| **WatchDog** | `WatchDog/App.cs` | 887 | Main ModTool application — XML asset collection management, asset filtering/querying, mod composition, spline editing, multi-language translation dictionary, game file watching |
| **RDArchiver** | `RDArchiver/App.cs` | 127 | RDA archive extraction CLI — processes `data*.rda` files sequentially, pattern-based file filtering |
| **Newtonsoft.Json** | ~530 files | ~60,000+ | Decompiled copy of the Newtonsoft.Json library (JSON serialization) |

---

## What was adapted into this project

### 1. Core asset-splitting pipeline (`src/AssetSplitter/AssetProcessor.cs`)

**Origin:** `WatchDog/App.cs` — the asset extraction and XML processing logic

The WatchDog contained the original concepts for:
- Filtering `assets.xml` by template name lists
- Resolving `BaseAssetGUID` inheritance chains
- Building GUID → translated name dictionaries from `texts_*.xml`
- Writing per-asset output XML with `<ModOps>` / `<ModOp>` wrappers
- Organizing output by template folder
- Inserting translated GUID comment lines

All of this logic was rewritten from scratch in `AssetProcessor.cs` (`AssetProcessor` namespace, `Program` class, ~3,400 lines). The concepts are Pogobuckel's; the implementation is new:
- Targeting .NET 10 (was .NET Framework 4.6.1)
- Config-driven via external files in `config/` (was hardcoded arrays)
- Anno 117 support added (original was Anno 1800 only)
- Parallel processing with `Parallel.ForEach`
- External console message localization (11 languages)
- GUI integration via the UI runner/coordinator process-launch flow

### 2. RDA extraction CLI (`src/RDAExtract/App.cs`)

**Origin:** `RDArchiver/App.cs`

The `RDArchiver` namespace and CLI structure are directly derived. Both share:
- Namespace: `RDArchiver`
- Class: `App`
- Entry point pattern: parse args → list or extract → process RDA files

The rewrite modernizes it to .NET 10 and integrates it with the RDAExplorer library as a library dependency rather than a standalone binary.

---

## What was NOT carried over

### WatchDog features not present in this project

The WatchDog had significant functionality that was examined but not adapted:

| WatchDog feature | Reason not included |
|------------------|---------------------|
| `SortList` command — sort asset XML lists | Not relevant to ModOp extraction workflow |
| `ReplaceSplines` / `ReadSplines` — spline data editing | Game-specific spline feature, out of scope |
| `AddModToPck` — packaging mods into .pck archives | Distribution packaging, not extraction |
| `compose` command — composing mod collections | Mod composition tool, separate concern |
| `show_all` / `add_asset` / `create_collection` | Collection management, not extraction |
| `find_items_by_target` — querying by target GUID | Query tool, not needed in batch pipeline |
| Hardcoded static arrays for asset types (Products, Farms, Factories, Items, Vehicles, Public buildings, Skins) | Replaced by config-driven `config/01_Templates/` files |
| File watching / real-time update detection | Not needed in a batch extraction tool |

### Newtonsoft.Json (530 files) — not used at all

The decompiled package included a full copy of Newtonsoft.Json. This project does not use Newtonsoft.Json; all JSON handling uses `System.Text.Json` (built into .NET 10).

---

## RDAExplorer — separate attribution

The RDA archive reading library (`src/RDAExplorer/`) is **not** from Pogobuckel's ModTool. It comes from:

**Lysann Tranvouez's [RDAExplorer](https://github.com/lysanntranvouez/RDAExplorer)**

Integrated as source code; lightly adapted for this project (namespace kept as `RDAExplorer`, minor .NET modernisation). See [CREDITS.md](../CREDITS.md) for full attribution.

---

## Version delta

| Aspect | Original ModTool (WatchDog) | This repo (v1.0.0) |
|--------|----------------------------|---------------------|
| Framework | .NET Framework 4.6.1 | .NET 10 |
| Anno support | Anno 1800 only | Anno 1800 + Anno 117 |
| Templates / fixlists | Hardcoded static arrays | External `.txt` config files |
| Console messages | Hardcoded English strings | External JSON, 11 languages |
| UI | Console-only | Avalonia cross-platform GUI + console backend |
| Processing | Sequential | Parallel (`Parallel.ForEach`, CPU-1 threads) |
| Config | None | Full `config/` folder (templates, fixlists, regional ingredients, whitelists, app settings) |
| JSON library | Newtonsoft.Json (bundled) | System.Text.Json (built-in) |

---

## Decompilation tools used

- **ILSpy 10.0 Preview 2** — [github.com/icsharpcode/ILSpy](https://github.com/icsharpcode/ILSpy/releases) — used to reconstruct C# source from the compiled ModTool binaries

---

## Code style notes (for contributors)

Code paths that were reconstructed from decompilation and not yet fully refactored may still show:

- Generic variable names (`str1`, `flag1`, `num`) — rename when touching surrounding code
- Incomplete or missing nullable annotations — add `#nullable enable` and address with fixes or suppressions
- Minimal XML doc comments — add `<summary>` on public types/members when modifying

These are tracked in `.cursor/rules/decompiled-code-cleanup.mdc`.

---

*This project is not part of the ModTool package and is not affiliated with the anno-mods organization. See [CREDITS.md](../CREDITS.md) for full attribution and [docs/LEGAL.md](LEGAL.md) for compliance notes.*
