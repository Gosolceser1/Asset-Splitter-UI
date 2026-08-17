# PngToIco

Small .NET tool that converts a PNG image to a multi-size Windows ICO (16, 32, 48, 256) using SkiaSharp. Used to generate `app-icon.ico` for AssetSplitterUI.

**Usage**

```bash
dotnet run -- <input.png> <output.ico>
# Default: app-icon.png → app-icon.ico
```

From the repo root, use the wrapper script:

```powershell
.\scripts\png-to-ico.ps1
# Or with custom paths:
.\scripts\png-to-ico.ps1 -PngPath "path\to\image.png" -IcoPath "path\to\output.ico"
```

**Requires:** .NET (e.g. net10.0), SkiaSharp (see `PngToIco.csproj`).
