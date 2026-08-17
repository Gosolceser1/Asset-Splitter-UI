$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$locales = @("de","es","fr","it","ja","ko","pl","ru","tw","zh")

# Patterns that indicate encoding corruption
$corruptPatterns = @(
    'â€',       # UTF-8 read as Latin-1
    'Ã¼',       # ü corrupted
    'Ã¶',       # ö corrupted
    'Ã¤',       # ä corrupted
    'Ã©',       # é corrupted
    'Ã¨',       # è corrupted
    'Ã ',       # à corrupted
    'Ã®',       # î corrupted
    'Ã´',       # ô corrupted
    'Ã»',       # û corrupted
    'Ã§',       # ç corrupted
    'â€™',      # ' corrupted
    'â€œ',      # " corrupted
    'â€"',      # — corrupted
    'â€¦',      # … corrupted
    'Ã',        # Generic Ã prefix (catch-all)
    'â€',       # Generic â€ prefix
    '\?{2,}',   # Multiple question marks (encoding failure)
    '[\x00-\x08\x0B\x0C\x0E-\x1F]'  # Control characters (except tab/newline)
)

# Known placeholder tokens - NOT corruption
$knownPlaceholders = @('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}')

foreach ($loc in $locales) {
    $file = "$base\console_$loc.json"
    $raw = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
    $issues = @()
    
    foreach ($pattern in $corruptPatterns) {
        $matches = [regex]::Matches($raw, $pattern)
        foreach ($m in $matches) {
            # Get context around the match
            $start = [Math]::Max(0, $m.Index - 40)
            $len   = [Math]::Min(100, $raw.Length - $start)
            $ctx   = $raw.Substring($start, $len) -replace "`n"," " -replace "`r",""
            $issues += "  PATTERN '$pattern' at pos $($m.Index): ...${ctx}..."
        }
    }
    
    if ($issues.Count -gt 0) {
        Write-Host "=== $loc === $($issues.Count) potential corruption(s):" -ForegroundColor Red
        $issues | Select-Object -Unique | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "=== $loc === CLEAN" -ForegroundColor Green
    }
}
Write-Host "`nDone."
