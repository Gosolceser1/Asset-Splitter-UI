# Legal & community compliance

This document summarizes how this project aligns with Anno modding community norms and publisher terms, so the project stays within accepted practice and avoids takedown risk.

## How the Anno modding community handles licenses

The **[anno-mods](https://github.com/anno-mods)** organization and related projects use a mix of permissive licenses; there is no single "anno-mods license." Common patterns:

| Project type | Typical license | Notes |
|--------------|-----------------|--------|
| Standalone tools (asset-extractor, vscode-anno) | **MIT** | Permissive; matches our choice. |
| Blender add-ons (Blender-Anno-117, Blender-Anno-1800) | **GPL-3.0** | Required by Blender's license. |
| Shared resources, GuidRanges, modding-guide | **MIT** or similar | Community docs and data. |

**Trademark / affiliation disclaimer:** Projects like [vscode-anno](https://github.com/anno-mods/vscode-anno) and other community tools include a notice that the project is not an official Ubisoft product and that **Anno is a trademark of Ubisoft**. We do the same in [LICENSE](../LICENSE).

**Third-party attribution:** Kept in [CREDITS.md](../CREDITS.md) for Pogobuckel (AssetSplit concept and RDA archive library). Bundled third-party tools are noted in their respective directories.

## Ubisoft terms and modding

- **Official mod support:** Ubisoft provides official mod support for both Anno 1800 and Anno 117 via mod.io. Modding tools that help users create and install mods are part of this supported ecosystem.
- **Non-commercial:** Ubisoft's terms limit use to individual, non-commercial, entertainment purposes. This tool is free and open source. Selling or commercially exploiting Ubisoft-derived content requires permission.
- **Derivative works / UGC:** The EULA restricts derivative works except when the product enables creating/submitting User Generated Content (official mod support). Community tools that *process the user's own game files* are not redistributing Ubisoft assets.
- **Trademark:** Do not imply endorsement or affiliation. The Ubisoft/Anno disclaimer in [LICENSE](../LICENSE) follows the community standard used by vscode-anno, RDAExplorer, and others.

## What this project does to stay compliant

1. **LICENSE**
   - Ubisoft/Anno disclaimer at top (community project; not affiliated; Anno = Ubisoft trademark).
   - MIT for this codebase (free to use, modify, distribute).
   - Third-party attribution with links to original projects.

2. **No redistribution of game assets**
   - The tool only reads the user's own game files (RDA) and outputs ModOp XML on the user's machine. We do not ship or host any Ubisoft assets.
   - Config files (templates, fixlists, ingredients, whitelists) contain only GUIDs and names — no game code or copyrighted data.

3. **Non-commercial framing**
   - The project is free and open source. README and docs do not promote selling the tool or selling Ubisoft-derived content.

4. **Attribution**
   - [CREDITS.md](../CREDITS.md) clearly attributes AssetSplit and the RDA archive library to Holger Eilts (Pogobuckel).
   - [MODTOOL-DECOMPILATION-ATTRIBUTION.md](MODTOOL-DECOMPILATION-ATTRIBUTION.md) documents the decompilation lineage of adapted code.

5. **Community alignment**
   - Same disclaimer style as vscode-anno and similar tools; MIT like asset-extractor and other standalone tools.
   - Follows the same license and disclaimer norms as the anno-mods organization.

## Third-party tools bundled

This repository includes the following third-party tools:

| Tool | Path | License | Source |
|------|------|---------|--------|
| texconv | `scripts/agent-tools/texconv.exe` | MIT | [Microsoft/DirectXTex](https://github.com/microsoft/DirectXTex) |
| RDAExplorer / ZLib | `src/RDAExplorer/` | Pogobuckel (ModTool 5.55) | Decompiled and rewritten from ModTool.dll; ZLib.cs uses managed System.IO.Compression (no native zlib.dll required) |
| JetBrains Mono fonts | `src/AssetSplitterUI/Assets/Fonts/` | OFL-1.1 | [JetBrains](https://www.jetbrains.com/lp/mono/) |

## Summary

We use **MIT**, an **Ubisoft/Anno disclaimer** in LICENSE, and **CREDITS.md** for third-party attribution. We do not redistribute game assets, claim affiliation with Ubisoft, or encourage commercial use of Ubisoft content. This matches how other Anno modding tools operate and should keep the project in line with community practice and publisher expectations.

For the full license text, see [LICENSE](../LICENSE). For third-party and original-work credits, see [CREDITS.md](../CREDITS.md).
