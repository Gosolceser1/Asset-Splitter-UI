$base = "C:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages"
$de = Get-Content "$base\console_de.json" -Raw | ConvertFrom-Json
Write-Host "readmeGuidePublish2:"
Write-Host $de.readmeGuidePublish2
Write-Host ""
Write-Host "readmeGuideQuickStep4:"
Write-Host $de.readmeGuideQuickStep4
Write-Host ""
Write-Host "readmeWhatIsFlow:"
Write-Host $de.readmeWhatIsFlow
