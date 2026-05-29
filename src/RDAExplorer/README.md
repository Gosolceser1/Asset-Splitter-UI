# RDAExplorer

Library for reading Anno **RDA** (Resource Data Archive) files — V2.0 / V2.2 headers, zlib blocks, optional XOR encryption, memory-resident blocks.

Used by **AssetProcessor** (Phase 1) and the **RDAExtract** CLI.

## Key types

| Type | Role |
|------|------|
| `RDAReader` | Opens archive, reads block chain |
| `RDAFolder` / `RDAFile` | Tree and file entries |
| `FileHeader`, `DirEntry`, `BlockInfo` | On-disk structures |
| `RDAFileExtension` | `ExtractAll`, `ListAll`, high-level helpers |
| `RDAMemoryResidentHelper` | Contiguous compressed block payloads |

## Subfolders

| Folder | Role |
|--------|------|
| [Misc/](Misc/) | Binary/date helpers (`BinaryExtension`, etc.) |
| [ZLib/](ZLib/) | Deflate decompression for compressed blocks |

## Format reference

Community wiki: [RDA File Format](https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format)

PowerShell exploration scripts (separate from this DLL): [scripts/agent-tools/README.md](../../scripts/agent-tools/README.md)

Solution overview: [src/README.md](../README.md)
