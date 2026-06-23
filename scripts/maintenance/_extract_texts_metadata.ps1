. (Join-Path $PSScriptRoot "..\agent-tools\Read-RDA.ps1")

$config = "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana\maindata\config.rda"
$metaPath = "data/base/config/gui/texts_metadata.xml"
$outPath = "C:\Users\vadim\Desktop\AnnoAssets\Anno117\source_xml_anno117\texts_metadata.xml"

Write-Host "Extracting $metaPath ..."
$content = Read-RDAFile -Path $config -FileName $metaPath
[System.IO.File]::WriteAllText($outPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $outPath ($($content.Length) chars)"
Write-Host ""
Write-Host "=== First 3000 chars ==="
Write-Host $content.Substring(0, [Math]::Min(3000, $content.Length))

$ids = @(
    "6914369542672157320",
    "6908121430051582848",
    "6901408648429238493",
    "6905806747125135170",
    "6913571162315191570"
)

Write-Host ""
Write-Host "=== Search missing LineIds in metadata ==="
foreach ($id in $ids) {
    if ($content -match $id) {
        Write-Host "FOUND: $id"
    } else {
        Write-Host "missing: $id"
    }
}
