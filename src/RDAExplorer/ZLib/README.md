# ZLib (RDAExplorer)

**Zlib/deflate decompression** for RDA blocks flagged as compressed (`0x0001`).

Wrapped by `RDAReader` when reading file payloads and compressed directory headers.

Derived from Pogobuckel's `ModTool.ZLib.ZLib`. The original used native `[DllImport("zlib.DLL")]`; this implementation uses managed `System.IO.Compression.ZLibStream` (no native dependency).

Parent: [RDAExplorer README](../README.md) · Format: [RDA wiki](https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format) (reference only)
