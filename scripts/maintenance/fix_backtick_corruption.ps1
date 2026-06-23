$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$locales = @("de","es","fr","it","ja","ko","pl","ru","tw","zh")

# Map each locale's corrupted value -> correct value for each of the 3 affected keys
# The corruption: PowerShell escape `a = BEL(0x07), `r = CR(0x0D) etc. stripped the char after backtick
# In readmeGuidePublish2: `asset-splitter-...` had `a eaten -> needs backtick+asset-splitter
# In readmeGuideQuickStep4: `assets.xml` had `a eaten, `replace` had `r eaten
# In readmeWhatIsFlow: `assets.xml` had `a eaten, `modinfo.json` and `assets.xml` appear

# We fix by replacing the corrupted text (without backtick) with the correct backtick-wrapped text.
# Pattern: word missing leading backtick, surrounded by context

$fixes = @(
    # readmeGuidePublish2 - all locales have "sset-splitter-..." missing the backtick+a
    @{ Pattern = 'sset-splitter-\.\.\.`'; Replace = '`asset-splitter-...`' }
    # readmeGuideQuickStep4 - all locales have "ssets.xml`" and "eplace`"
    @{ Pattern = 'ssets\.xml`'; Replace = '`assets.xml`' }
    @{ Pattern = '`eplace`'; Replace = '`replace`' }
    # readmeWhatIsFlow - same ssets.xml corruption; also check modinfo.json (not corrupted, keep as is)
    # The `modinfo.json` backtick was not corrupted (m is not a PS escape) - only `a and `r were
)

foreach ($loc in $locales) {
    $file = "$base\console_$loc.json"
    $raw = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
    $changed = $false
    foreach ($fix in $fixes) {
        $newRaw = [regex]::Replace($raw, [regex]::Escape($fix.Pattern) -replace '\\\.', '.', $fix.Replace)
        # Use literal replacement instead
        if ($raw.Contains($fix.Pattern -replace '\\','')) {
            $raw = $raw.Replace(($fix.Pattern -replace '\\',''), $fix.Replace)
            $changed = $true
        }
    }
    if ($changed) {
        [System.IO.File]::WriteAllText($file, $raw, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Fixed: $loc"
    } else {
        Write-Host "No change needed: $loc"
    }
}
Write-Host "Done."
