# RDAExtract

Standalone CLI to **list** or **extract** files from Anno `.rda` archives. References the **RDAExplorer** library.

## When to use

| Use case | Tool |
|----------|------|
| Full game extraction pipeline | Asset Splitter UI / `AssetProcessor.exe` (integrated Phase 1) |
| Inspect one archive, quick extract | **RDAExtract** |
| PowerShell scripting / search | [scripts/agent-tools/](../../scripts/agent-tools/README.md) |

## Build and run

```bash
dotnet run --project src/RDAExtract/RDAExtract.csproj -- <path-to.rda> [options]
```

Run with no args or `-h` for built-in help (if implemented in project entry).

## Related

- [RDAExplorer/README.md](../RDAExplorer/README.md) — API and format
- [src/README.md](../README.md) — solution layout
