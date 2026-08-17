namespace AssetProcessor;

public static partial class ConsoleMessages
{
    private static void AddBannerDefaults(Dictionary<string, string> messages)
    {
        messages["bannerTitle"] = "│  ANNO ASSET SPLITTER v2.0 Enhanced Edition     │";
        messages["bannerAnno1800"] = "│  🎮 Anno 1800  ............ ✓ Supported        │";
        messages["bannerAnno117"] = "│  🎮 Anno 117 (Pax) ....... ✓ Supported        │";
        messages["readyToExtract"] = "│  Status: Ready to Extract                      │";
        messages["gameDetectedPrefix"] = "│  ✓ Detected Game: {0}";
        messages["gameNotDetected"] = "[ERROR] GAME NOT DETECTED";
        messages["bannerSubtitle"] = "│  Asset Extraction & ModOp Generator            │";
    }
}
