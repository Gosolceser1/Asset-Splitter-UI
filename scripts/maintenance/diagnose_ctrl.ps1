$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$raw = [System.IO.File]::ReadAllText("$base\console_de.json", [System.Text.Encoding]::UTF8)
$hits = [regex]::Matches($raw, '[\x00-\x08\x0B\x0C\x0E-\x1F]')
foreach ($h in $hits) {
    [int]$ch = [char]$raw[$h.Index]
    $start = [Math]::Max(0, $h.Index - 20)
    $ctx = $raw.Substring($start, 50) -replace "`r","" -replace "`n"," "
    Write-Host ("Pos {0}: char 0x{1:X2} context: '{2}'" -f $h.Index, $ch, $ctx)
}
