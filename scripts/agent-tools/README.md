# Anno RDA File Reader Scripts (optional)

> **Not required** to build or run Asset Splitter UI. Optional PowerShell toolkit for exploring `.rda` archives outside the app.

PowerShell scripts for reading Anno 117 / Anno 1800 RDA archives. Complements the in-repo **RDAExplorer** C# library used by `AssetProcessor.exe`.

Based on the community [RDAExplorer](https://github.com/lysanntranvouez/RDAExplorer) project.

## Included Knowledge File

This folder now includes a portable archive reference: [RDA-KNOWLEDGE.md](RDA-KNOWLEDGE.md).

Use it when you want the key archive map, script-system differences, search rules, and known hot paths without relying on Copilot local memory.

## Archive Precedence Model

This is the key behavioral difference between games and applies to all extracted files, not only datasets.

- **Anno 1800**: Higher-numbered `data*.rda` archives are newer and override lower-numbered archives.
- **Anno 117**: Archive layout is organized by archive purpose and does not rely on the same numeric override pattern.

### Dataset example (verified)

These dataset paths were verified from local game archives using `Read-RDA.ps1`.

- **Anno 1800**: `data/config/export/main/asset/datasets.xml`
- **Anno 117**: `data/base/config/game/datasets.xml`

### Quick dataset lookup commands

```powershell
. .\Read-RDA.ps1

# Anno 117
$rda117 = "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana\maindata\config.rda"
Get-RDAFileList -Path $rda117 -Filter "datasets\.xml"

# Anno 1800 (scan all data*.rda)
$root1800 = "C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800\maindata"
Get-ChildItem $root1800 -Filter "data*.rda" | ForEach-Object {
    try {
        $hits = Get-RDAFileList -Path $_.FullName -Filter "datasets\.xml"
        if ($hits) {
            [pscustomobject]@{ RDA = $_.Name; Path = $hits }
        }
    } catch {}
}
```

## Quick Start

```powershell
# Load quick commands
. .\RDA-Agent.ps1

# List all RDA files in the game
rda-all

# List files in config.rda matching "assets"
rda-list config "assets"

# Get block info for an RDA
rda-blocks ui

# Search for text in config.rda
rda-search config "BlacklistFeature"

# Read a file from an RDA
rda-read config "data/base/config/export/assets.xml"

# Extract a file to disk
rda-extract config "data/base/config/export/assets.xml" "assets.xml"

# Extract an image and convert it to PNG
rda-image ui "data/ui/.../your_icon_0.dds" ".\your_icon.png"
```

## Extracting UI Icons

Most UI icons in Anno 117 are stored in `ui.rda` as `.dds` textures, often with a `_0.dds` suffix, even when XML paths refer to `.png`-style names.

This folder now includes a bundled `texconv.exe`, and `RDA-Agent.ps1` exposes it through the `rda-image` helper.

### 1. Find the icon path

```powershell
# Load quick commands
. .\RDA-Agent.ps1

# Search the UI archive for part of an icon name
rda-list ui "your-search-term"
```

Replace `your-search-term` with whatever you are looking for, such as part of an item name, resource name, or icon filename.

Example matches might look like:

```text
data/ui/4k/dlc01/icon_content/generic/icon_2d_example_0.dds
data/ui/4k/dlc01/icon_content/production_goods/icon_3d_example_goods_0.dds
```

### 2. Extract the icon as DDS

```powershell
rda-extract ui "data/ui/.../your_icon_0.dds" ".\your_icon.dds"
```

`rda-extract` uses `Read-RDAFile -AsBytes`, so it is safe for binary files such as `.dds`, `.png`, `.jpg`, `.wem`, etc.

### 3. Extract and convert in one step

```powershell
rda-image ui "data/ui/.../your_icon_0.dds" ".\your_icon.png"
```

Supported output formats in the helper are `png`, `jpg`, `tga`, and `bmp`.

### Convert DDS to PNG

Recommended: use the bundled `texconv.exe` from Microsoft's DirectXTex tools, because DDS support is reliable.

```powershell
texconv.exe -ft png -o . .\your_icon.dds
```

This creates `your_icon.png` in the current folder.

Other options:

- Paint.NET: open the `.dds` file and save as `.png`
- GIMP: open the `.dds` file and export as `.png`
- ImageMagick: works in some setups, but DDS support depends on the installed codecs/build

### Scripted extraction example

```powershell
. .\Read-RDA.ps1

$rda = "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana\maindata\ui.rda"
$file = "data/ui/.../your_icon_0.dds"
$bytes = Read-RDAFile -Path $rda -FileName $file -AsBytes
[System.IO.File]::WriteAllBytes(".\your_icon.dds", $bytes)
```

Use this form if you want direct control over the extraction step.

## Scripts

### Read-RDA.ps1

The main RDA reader module. Contains these functions:

| Function | Description |
|----------|-------------|
| `Get-RDAInfo` | Get basic information about an RDA file (size, blocks, files, compression) |
| `Get-RDABlocks` | Get detailed information about each block (flags, offsets, file counts) |
| `Get-RDAFileList` | List all files in an RDA archive (supports compressed/encrypted headers) |
| `Read-RDAFile` | Read file contents (supports compression, encryption, memory-resident) |
| `Search-RDAContent` | Search for text within files in an RDA |
| `Get-AllRDAFiles` | List all RDA files in the game's maindata folder |

### Helper Functions

| Function | Description |
|----------|-------------|
| `Get-DecryptionSeed` | Get the LCG seed for encryption (0x71C71C71 for V2.2) |
| `Decrypt-RDAData` | Decrypt data using Anno's XOR cipher |
| `Decompress-ZlibData` | Decompress zlib/deflate data |

### RDA-Agent.ps1

Quick helper commands with shorter syntax:

| Command | Description |
|---------|-------------|
| `rda-all` | List all RDA files with file counts and compression status |
| `rda-info <name>` | Get info about an RDA (e.g., `rda-info config`) |
| `rda-blocks <name>` | Get block details (e.g., `rda-blocks ui`) |
| `rda-list <name> [filter]` | List files (e.g., `rda-list config "assets"`) |
| `rda-read <name> <file>` | Read file contents |
| `rda-search <name> <pattern>` | Search for text (regex) |
| `rda-extract <name> <file> [output]` | Extract file to disk |
| `rda-image <name> <file> [output]` | Extract image to a temp DDS and convert it with bundled texconv |

## RDA File Format (V2.2)

Based on: https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format

Anno 117 uses **Resource File V2.2** format (same as Anno 2205 and Anno 1800).

### Header (792 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 18 | Magic: "Resource File V2.2" (UTF-8) |
| 18 | 766 | Unknown (always 0) |
| 784 | 8 | First block offset (int64) |

### Block Structure

Blocks are stored as a linked list. Each block contains:
1. File data (variable size per file)
2. File headers (560 bytes each)
3. Optional memory-resident info (16 bytes) if flag set
4. Block header (32 bytes)

#### Block Header (32 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 4 | Flags (bitmask) |
| 4 | 4 | Number of files |
| 8 | 8 | Compressed header size |
| 16 | 8 | Uncompressed header size |
| 24 | 8 | Next block offset |

#### Block Flags

| Flag | Value | Description |
|------|-------|-------------|
| Compressed | 0x0001 | Block uses zlib compression |
| Encrypted | 0x0002 | Block uses XOR encryption |
| Memory-Resident | 0x0004 | File data is contiguous/compressed together |
| Deleted | 0x0008 | Block is deleted, skip it |

#### File Header (560 bytes)

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 520 | File path (UTF-16, null-terminated) |
| 520 | 8 | Data offset (absolute, or relative if memory-resident) |
| 528 | 8 | Compressed file size |
| 536 | 8 | Uncompressed file size |
| 544 | 8 | Timestamp |
| 552 | 8 | Unknown (always 0) |

### Memory-Resident Info (16 bytes, optional)

Located before block header when FLAG_MEMORY_RESIDENT is set:

| Offset | Size | Description |
|--------|------|-------------|
| 0 | 8 | Compressed data size |
| 8 | 8 | Uncompressed data size |

### Encryption

RDA uses a Linear Congruential Generator (LCG) based XOR cipher:

```
seed = 0x71C71C71  (for V2.2)
for each 16-bit word:
    seed = (seed * 214013 + 2531011) mod 2^32
    xor_key = (seed >> 16) & 0x7FFF
    decrypted_word = encrypted_word XOR xor_key
```

### Version Differences

| Feature | V2.0 (Anno 1404/2070) | V2.2 (Anno 2205/1800/117) |
|---------|----------------------|---------------------------|
| Magic | UTF-16 | UTF-8 |
| Header size | 1048 bytes | 792 bytes |
| First block offset | Position 1044, 4 bytes | Position 784, 8 bytes |
| Size fields | 4 bytes (int32) | 8 bytes (int64) |
| File header size | 540 bytes | 560 bytes |
| Block header size | 20 bytes | 32 bytes |
| Encryption seed | 0xA2C2A | 0x71C71C71 |

## Anno 117 RDA Files

**Note:** Anno 117 does NOT use compression or encryption for blocks — all are uncompressed and unencrypted.

| File | Description | Files | Size |
|------|-------------|-------|------|
| config.rda | Game configuration (assets.xml, texts, engine.ini, console, balancing) | 171 | 120 MB |
| shared_configs.rda | Shared graphics configs (.bfg/.cfg/.ifo files) | 32,314 | 2.5 GB |
| script.rda | Lua scripts (core, content, types, modules, cheats) | 1,031 | 9 MB |
| infotips.rda | InfoTips export.bin | 1 | 2 MB |
| ui.rda | UI layout, assets, images, 2K/4K textures | 17,866 | 7.8 GB |
| graphics_roman.rda | Roman building models/textures | 20,925 | 26.8 GB |
| graphics_celtic.rda | Celtic building models/textures | 8,702 | 7.0 GB |
| graphics_roman_celtic.rda | Shared Roman+Celtic models | 2,473 | 8.1 GB |
| graphics_library.rda | Shared graphics library (clouds, ambient, common) | 20,919 | 8.7 GB |
| graphics_portrait.rda | Character portraits (specialists, NPCs) | 1,004 | 7.1 GB |
| graphics_skins.rda | Building skin variants | 1,676 | 1.4 GB |
| graphics_engine.rda | Engine-level graphics | 177 | 81 MB |
| graphics_ui.rda | UI-specific graphics | 1,894 | 687 MB |
| graphics_misc.rda | Miscellaneous graphics | 42 | 11 MB |
| cdlc01_graphics.rda | DLC01 graphics (ornaments, wall system, mosaic skins) | 1,862 | 2.3 GB |
| dlc01_graphics.rda | DLC01 graphics under data/dlc01/ path | 1,586 | 3.2 GB |
| dlc01_provinces.rda | DLC01 province data (Latium/Volcano) | 8,837 | 1.4 GB |
| shaders.rda | HLSL shaders (DX12) | 30,662 | 12.2 GB |
| provinces_roman.rda | Roman province data (.prp, terrain) | 29,545 | 5.2 GB |
| provinces_celtic.rda | Celtic province data (.prp, terrain) | 30,810 | 4.4 GB |
| video.rda | Cutscene videos (.bk2 Bink) | 135 | 17.7 GB |
| sound.rda | Audio files (.wem Wwise) | 9,704 | 680 MB |
| file_browse_patterns.rda | File browser pattern definitions | 138 | 8 MB |
| en_us0.rda | English localization | 12,835 | 460 MB |
| de_de0.rda | German localization | 13,957 | 510 MB |
| fr_fr0.rda | French localization | 13,954 | 496 MB |
| zh_cn0.rda | Chinese localization | 21,308 | 709 MB |
| zz_patchfiles_150.rda | Patch v150 overrides (config, scripts, DLC configs) | 6,480 | 2.6 GB |
| zz_patchfiles_151.rda | Patch v151 incremental update | 55 | 37 MB |

## Capabilities

| Feature | Status |
|---------|--------|
| Read V2.2 format | ✅ Supported |
| Read V2.0 format | ⚠️ Detection only |
| List files | ✅ Supported |
| Read uncompressed files | ✅ Supported |
| Zlib decompression | ✅ Supported |
| Compressed block headers | ✅ Supported |
| XOR Decryption | ✅ Supported |
| Memory-resident blocks | ✅ Supported |
| Search file contents | ✅ Supported |
| Extract files | ✅ Supported |

## References

- [RDA File Format Wiki](https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format)
- [RDA Explorer Tool](https://github.com/lysanntranvouez/RDAExplorer)
- [RDAExplorer Source - BinaryExtension.cs](https://github.com/lysanntranvouez/RDAExplorer/blob/main/src/RDAExplorer/Misc/BinaryExtension.cs) (Decryption)
- [RDAExplorer Source - RDAReader.cs](https://github.com/lysanntranvouez/RDAExplorer/blob/main/src/RDAExplorer/RDAReader.cs) (Block reading)
- [RDAExplorer Source - RDAMemoryResidentHelper.cs](https://github.com/lysanntranvouez/RDAExplorer/blob/main/src/RDAExplorer/RDAMemoryResidentHelper.cs) (Memory-resident)
