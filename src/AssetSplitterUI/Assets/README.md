# Assets

Static resources embedded or copied with **AssetSplitterUI**.

| Item | Purpose |
|------|---------|
| `app-icon.png` | Source artwork for the application icon |
| `app-icon.ico` | Windows multi-size icon (16, 32, 48, 256) |
| [Fonts/](Fonts/) | JetBrains Mono — console log and monospace UI |

## Regenerating the icon

```powershell
.\scripts\png-to-ico.ps1
# Custom paths:
.\scripts\png-to-ico.ps1 -PngPath "src\AssetSplitterUI\Assets\app-icon.png" -IcoPath "src\AssetSplitterUI\Assets\app-icon.ico"
```

Implementation: [scripts/PngToIco/](../../../scripts/PngToIco/README.md).

Fonts are referenced in `App.axaml` / theme resources; keep file names stable when upgrading font files.
