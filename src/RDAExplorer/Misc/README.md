# Misc (RDAExplorer)

Low-level helpers for parsing RDA binary data.

| Type | Purpose |
|------|---------|
| `BinaryExtension` | LCG XOR decryption cipher used by `RDAReader`. Version-specific seeds: 666666 (V2.0) and 1908874353 (V2.2). Derived from Pogobuckel's `ModTool.Misc.BinaryExtension`. |
| `DateTimeExtension` | Converts RDA timestamps to `DateTime`. |

Not invoked directly from the UI — only from **RDAExplorer** and **RDAExtract**.

Parent: [RDAExplorer README](../README.md).
