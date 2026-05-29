# Create app-icon.ico from app-icon.png for Windows .exe icon.
# Uses multi-size .ico (16, 32, 48, 256) so Explorer shows the icon correctly.
param(
    [string]$PngPath = "$PSScriptRoot\..\src\AssetSplitterUI\Assets\app-icon.png",
    [string]$IcoPath = "$PSScriptRoot\..\src\AssetSplitterUI\Assets\app-icon.ico"
)
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$toolProj = "scripts\PngToIco\PngToIco.csproj"
if (Test-Path "$repoRoot\$toolProj") {
    Push-Location $repoRoot
    try {
        & dotnet run --project $toolProj -- $PngPath $IcoPath
        if ($LASTEXITCODE -eq 0) { exit 0 }
    } finally { Pop-Location }
}
# Fallback: single-size .ico (Explorer may not show it well)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($PngPath)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::Create($IcoPath)
$icon.Save($fs)
$fs.Close()
$icon.Dispose()
$bmp.Dispose()
Write-Host "Created $IcoPath (single size; run 'dotnet run --project scripts\PngToIco' for multi-size)"
