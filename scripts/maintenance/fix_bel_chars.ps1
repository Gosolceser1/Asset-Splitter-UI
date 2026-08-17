$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$locales = @("de","es","fr","it","ja","ko","pl","ru","tw","zh")

$bel = [char]0x07

foreach ($loc in $locales) {
    $file = "$base\console_$loc.json"
    $raw = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

    $before = $raw

    # BEL + "sset" -> backtick + "asset"  (covers `asset-splitter-...` and `assets.xml`)
    $raw = $raw.Replace("${bel}sset", '`asset')

    # Also check for any other BEL-corrupted backtick sequences
    # `replace` -> BEL + "replace" (0x07 + r is NOT a PS escape — `r = CR, so `r gets eaten differently)
    # `r = carriage return (0x0D) -- but 0x0D is not in our control char pattern for \x0B\x0C range
    # Let's check if `replace was also corrupted (it would be 0x0D + "eplace" -> but CR is common)
    # From earlier output: "eplace`" appears -> means the backtick before "replace" became something
    # Actually `r in PS = CR (0x0D). Let's check for CR + "eplace"
    $cr = [char]0x0D
    $raw = $raw.Replace("${cr}eplace", '`replace')

    # Verify no more BEL chars remain
    $remaining = [regex]::Matches($raw, '[\x00-\x08\x0B\x0C\x0E-\x1F]')
    if ($remaining.Count -gt 0) {
        Write-Host "WARNING: $loc still has $($remaining.Count) control char(s) after fix"
        foreach ($h in $remaining) {
            $start = [Math]::Max(0, $h.Index - 15)
            $ctx = $raw.Substring($start, 40) -replace "`r","<CR>" -replace "`n","<LF>"
            Write-Host ("  pos {0}: 0x{1:X2} -> '{2}'" -f $h.Index, ([int][char]$raw[$h.Index]), $ctx)
        }
    }

    if ($raw -ne $before) {
        [System.IO.File]::WriteAllText($file, $raw, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Fixed: $loc"
    } else {
        Write-Host "No BEL found: $loc"
    }
}
Write-Host "Done."
