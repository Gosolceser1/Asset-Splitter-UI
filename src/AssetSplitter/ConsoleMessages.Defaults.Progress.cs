namespace AssetProcessor;

public static partial class ConsoleMessages
{
    private static void AddProgressDefaults(Dictionary<string, string> messages)
    {
        messages["buildingGuidIndex"] = "Building GUID index...";
        messages["readingLanguageFile"] = "reading language file";
        messages["readingPropertiesFile"] = "reading properties file...";
        messages["extractingAssets"] = "extracting assets...";
        messages["extractingProgress"] = "Extracting...";
        messages["mergingProgress"] = "Merging...";
        messages["formattingAssets"] = "formatting assets...";
        messages["formattingProgress"] = "Formatting...";
        messages["preparingFormatting"] = "Preparing final formatting pass - scanning output directory...";
        messages["preparingFormattingWithCount"] = "Preparing final formatting pass - scanning output directory... {0} files found";
        messages["completedLabel"] = "completed";
        messages["done"] = "done.";
        messages["doneLabel"] = "[DONE]";
        messages["resolvingDependencies"] = "Resolving BaseAssetGUID dependencies for";
        messages["extractingGameData"] = "Extracting game data from RDA archives...";
        messages["buildingAssetRegistry"] = "Building asset name registry from game database...";
        messages["preparingBaseAssetGuid"] = "Preparing BaseAssetGUID reference pass...";
        messages["resolvingDependenciesSummary"] = "Resolving dependencies";
        messages["scanningDependenciesSummary"] = "Scanning dependencies";
        messages["extractingToModOp"] = "Extracting {0} assets to ModOp format...";
        messages["extractingToXml"] = "Extracting {0} assets to XML...";
        messages["resolvingDepsFor"] = "Resolving BaseAssetGUID dependencies for {0} assets...";
        messages["assetRegistryBuilding"] = "[ASSETS] Building asset registry from game's master asset database...";
        messages["mergingInheritedProperties"] = "[FIX] Merging inherited properties from parent assets";
        messages["resolvingAssetProgress"] = "Resolving: {0} <- {1}";
        messages["scanningAssetProgress"] = "Scanning: {0}";
        messages["finalProcessingCompleted"] = "[COMPLETE] Final processing completed:";
        messages["processingAssetProgress"] = "Processing: {0}";
        messages["translationDictionaryBuilding"] = "[INFO] Building dictionary to convert numeric GUIDs into readable names";
        messages["lineIdContextBuilding"] = "[TRANS] Building LineId context index from asset graph...";
        messages["assetExtractionSuccess"] = "[SUCCESS] ✓ Asset extraction completed successfully!";
        messages["mergingAssetProgress"] = "Merging: {0}";
        messages["autoTemplatesExtractingAll"] = "[AUTO-TEMPLATES] Extracting all templates from game files...";
        messages["assetModsProgress"] = "Creating asset mods";
        messages["formattingRunSummary"] = "[INFO] Formatting summary: {0} files annotated ({1} comments), {2} template moves, {3} missing Template node";
        messages["modExportBuildingPackages"] = "Building asset mod folders from {0} extracted assets...";
        messages["progressExtracting"] = "Extracting: {0} - {1} ({2})";
        messages["progressMerging"] = "Merging: {0}";
        messages["assetModsProgressNonDebug"] = "Created {0} asset mod packages so far...";
    }
}
