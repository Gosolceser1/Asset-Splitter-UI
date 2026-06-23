$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$raw = [System.IO.File]::ReadAllText("$base\console_de.json", [System.Text.Encoding]::UTF8)

# Check all control chars including CR (0x0D) in the vicinity of the known corrupted keys
$keys = @("readmeGuidePublish2", "readmeGuideQuickStep4", "readmeWhatIsFlow")
foreach ($k in $keys) {
    $idx = $raw.IndexOf("`"$k`"")
    if ($idx -ge 0) {
        $segment = $raw.Substring($idx, [Math]::Min(300, $raw.Length - $idx))
        Write-Host "=== $k ==="
        # Show hex dump of first 200 chars of value
        foreach ($i in 0..([Math]::Min(199, $segment.Length-1))) {
            $c = [int][char]$segment[$i]
            if ($c -lt 0x20 -and $c -ne 0x0A) {
                Write-Host ("  pos+{0}: 0x{1:X2} (control char)" -f $i, $c)
            }
        }
        # Show the value readable
        $display = $segment -replace "[`u{0000}-`u{001F}]","<CTRL>"
        Write-Host $display.Substring(0, [Math]::Min(250, $display.Length))
        Write-Host ""
    }
}
