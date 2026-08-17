namespace AssetProcessor;

public static partial class ConsoleMessages
{
    private static void AddRdaDefaults(Dictionary<string, string> messages)
    {
        messages["extractingFromRda"] = "extracting from RDA file(s)...";
        messages["rdaExtractionComplete"] = "[RDA COMPLETE] ✓ Game files extracted. Configure options in the GUI, then run again for asset processing";
        messages["rdaExtractionSuccess"] = "[SUCCESS] All language files extracted successfully!";
        messages["rdaExtractionInfo"] = "[INFO] Configure your options in the GUI (language and processing features), then run again for asset processing";
        messages["extractingFromRdaCompressed"] = "Extracting game data from compressed RDA archive files...";
        messages["rdaAnno117Detected"] = "[RDA] Anno 117 detected - decompressing assets.xml, templates.xml, properties.xml, properties-meta.xml, datasets.xml, audio_generated.xml...";
        messages["rdaExtractingAnno117CoreFiles"] = "[INFO] Extracting core game definition files from Anno 117 RDA archives";
        messages["rdaAnno1800Detected"] = "[RDA] Anno 1800 detected - decompressing assets.xml, templates.xml, properties-toolone.xml, datasets.xml...";
        messages["rdaExtractingAnno1800CoreFiles"] = "[INFO] Extracting core game definition files from Anno 1800 RDA archives";
        messages["rdaFilesContainAssets"] = "RDA files are container archives that store Anno game assets (some blocks/files may be compressed or encrypted)";
        messages["rdaDecompressionSuccessful"] = "[COMPLETE] RDA decompression successful - game data now accessible as XML";
        messages["rdaExtractionFailed"] = "[ERROR] RDA extraction failed: {0}";
        messages["rdaCompleteSelectLanguage"] = "[RDA COMPLETE] Game files extracted - Select language and run again for asset processing";
        messages["readmeWhatIsRda"] = "- **.rda archives**: The game stores all its data in compressed `.rda` files inside the `maindata` folder. You cannot edit those directly. Instead, you create mods that get merged on top at startup.";
        messages["rdaLongRunningNote"] = "This can take several minutes depending on your drive speed...\n";
        messages["rdaExtractionCompleteNonDebug"] = "RDA extraction complete — processed {0}/{1} archives in {2}";
        messages["rdaExtractionCompleteShort"] = "RDA extraction complete ({0}/{1} archives processed in {2})";
        messages["issueTitle_RdaExtractionFailed"] = "RDA extraction failed";
    }
}
