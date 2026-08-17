# Credits

This project builds on the work of others. The original ideas and implementations are not claimed as this project’s own; it is a standalone continuation for Anno 1800 and Anno 117.

**Attribution:** AssetSplit concept, original tool, and RDA archive library — **Holger Eilts (Pogobuckel)**.

---

## Holger Eilts (Pogobuckel) — Original AssetSplit

**Original author** of the AssetSplit concept and tool. All credit for the core idea and the original implementation belongs to him.

Holger Eilts († 6 December 2023), known in the Anno modding community as **Pogobuckel**, created AssetSplit as part of the ModTool collection. The console/backend (AssetSplitter) in this repository is derived from and extends his work. The in-app line *"Based on Pogobuckel's work"* refers to him. The workflow—extract assets, split into per-asset XML, resolve BaseAssetGUID, wrap in ModOps, add GUID comments—is his design. This project adds support for Anno 117, config-driven templates and fixlists, integrated RDA extraction, and a cross-platform UI.

**Original workflow:** Extract `assets.xml` (and related files) from the game into a folder such as `source_xml`. The splitter processes that file and writes one XML per asset into a template-based folder tree. Assets using `<BaseAssetGUID>` are filled from their parent so each file is self-contained. Output is wrapped in `<ModOps>` / `<ModOp>` with comment lines that translate GUIDs into readable names.

**Links**

| Resource | URL |
|----------|-----|
| Nexus profile | [Pogobuckel](https://www.nexusmods.com/profile/pogobuckel) |
| Original tool (Anno 1800) | [AssetSplit – Nexus Mods](https://www.nexusmods.com/anno1800/mods/140) |
| ModTool (Asset-Splitter is one of its tools) | [Anno 1800 ModTool](https://annomodtool.wixsite.com/main) |
| In memoriam | [Holger Eilts – Trauer-Lüneburg.de](https://www.trauer-lueneburg.de/traueranzeige/holger-eilts) |

*This repository is a standalone tool; it is not part of the ModTool package.*

---

## RDA archive library (`src/RDAExplorer/`)

The RDA (Resource Data Archive) reader and extraction library derives from the `ModTool.dll` library included in Pogobuckel's ModTool 5.55 package. This includes `RDAFile`, `RDAReader`, `RDAFolder`, `BlockInfo`, `DirEntry`, `FileHeader`, `BinaryExtension` (LCG XOR cipher, decryption seeds), and the ZLib decompression interface. All are credited to **Holger Eilts (Pogobuckel)**.

**Format reference:** The RDA binary format is documented on the community wiki maintained by Lysann Tranvouez: [RDA File Format wiki](https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format) — this is used as a reference document, not as source code.

---

*Asset Splitter — Anno 1800 & 117. Extract and process game assets for modding. Built on Pogobuckel’s AssetSplit and the RDAExplorer project.*
