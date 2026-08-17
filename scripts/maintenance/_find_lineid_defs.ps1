. (Join-Path $PSScriptRoot "..\agent-tools\Read-RDA.ps1")

$game = "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana\maindata"
$config = Join-Path $game "config.rda"

Write-Host "=== config.rda: files matching texts|gui|local|lang ==="
Get-RDAFileList -Path $config -Filter "texts" | Sort-Object
Get-RDAFileList -Path $config -Filter "gui/" | Where-Object { $_ -match "\.xml$" } | Sort-Object | Select-Object -First 40

$ids = @(
    "6914369542672157320",
    "6908121430051582848",
    "6901408648429238493",
    "6905806747125135170"
)

$archives = @(
    "config.rda",
    "zz_patchfiles_150.rda",
    "zz_patchfiles_151.rda",
    "infotips.rda",
    "script.rda",
    "en_us0.rda"
)

Write-Host ""
Write-Host "=== LineId full-archive grep ==="
foreach ($id in $ids) {
    Write-Host ""
    Write-Host "ID: $id"
    foreach ($a in $archives) {
        $p = Join-Path $game $a
        if (-not (Test-Path $p)) { continue }
        $hits = Search-RDAContent -Path $p -Pattern $id -FileFilter "\.(xml|lua|txt|json|ini|csv)$"
        if ($hits) {
            $hits | Select-Object -First 3 | ForEach-Object {
                Write-Host "  [$a] $($_.FileName)"
            }
            if ($hits.Count -gt 3) { Write-Host "  ... $($hits.Count) hits total" }
        }
    }
}
