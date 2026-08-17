using System.Globalization;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;

namespace AssetSplitterUI.Services;

internal static partial class ConsoleOutputLocalizer
{
    // Box-drawing characters output by the backend console. These are UTF-8 multi-byte
    // sequences (e.g. 0xE2 0x94 0x80 = U+2500 "─") incorrectly decoded as Windows-1252
    // by the Process.StandardOutput reader, producing garbled Unicode strings.
    // The constants below match the garbled form so box borders can be filtered out.
    // If the stdout reader encoding changes, these constants must be updated.
    private const string BoxDrawingPrefix = "\u00E2\u201D";
    private const string BoxHorizontal = "\u00E2\u201D\u20AC";

    private static readonly string[] IndentKeys =
    [
        "consoleMessages.phase1Summary",
        "consoleMessages.phase2Instructions",
        "consoleMessages.extractionComplete",
        "consoleMessages.assetsSaved"
    ];

    private static readonly Dictionary<string, string> ExactBackendKeys = new()
    {
        ["=== PHASE 1: RDA EXPLORER ==="] = "consoleMessages.backendPhase1Label",
        ["=== PHASE 2: ASSET SPLITTER ==="] = "consoleMessages.backendPhase2Label",
        ["=== PHASE 3: ASSET EXTRACTION ==="] = "consoleMessages.backendPhase3Label",
        ["=== PHASE 3B: SPLIT TEMPLATES ==="] = "consoleMessages.backendPhase3SplitLabel",
        ["=== PHASE 4: GUID FILE INDEX ==="] = "consoleMessages.backendPhase4GuidIndexLabel",
        ["=== PHASE 5: TEMPLATE INHERITANCE (MERGE) ==="] = "consoleMessages.backendPhase5MergeLabel",
        ["=== PHASE 6: DEPENDENCY RESOLUTION ==="] = "consoleMessages.backendPhase6DepsLabel",
        ["=== PHASE 7: FORMATTING ==="] = "consoleMessages.backendPhase7FormatLabel",
        ["=== PHASE 7: ASSET MOD PACKAGES ==="] = "consoleMessages.backendPhase7Label",
        ["=== PHASE 8: ASSET MOD PACKAGES ==="] = "consoleMessages.backendPhase8ModsLabel",
        ["Extracting game data from RDA archives..."] = "consoleMessages.extractingGameData",
        ["done."] = "consoleMessages.done",
        ["[SOURCE READY] Game files extracted to source_xml. Enable a processing option and run again to build asset XML."] = "consoleMessages.sourceExtractionComplete",
        ["[RDA COMPLETE] ✓ Game files extracted. Configure options in the GUI, then run again for asset processing"] = "consoleMessages.rdaExtractionCompleteReady",
        ["[RDA COMPLETE] Game files extracted - Select language and run again for asset processing"] = "consoleMessages.rdaCompleteSelectLanguage",
        ["[INFO] Configure your options in the GUI (language and processing features), then run again for asset processing"] = "consoleMessages.rdaExtractionInfo",
        ["Building asset name registry from game database..."] = "consoleMessages.buildingAssetRegistry",
        ["reading properties file..."] = "consoleMessages.readingPropertiesFile",
        ["Merging templates..."] = "consoleMessages.mergeTemplates",
        ["formatting assets..."] = "consoleMessages.formattingAssets",
        ["Preparing BaseAssetGUID reference pass..."] = "consoleMessages.preparingBaseAssetGuid",
        ["extracting assets..."] = "consoleMessages.extractingAssets",
        ["=== DEVELOPER ISSUE SUMMARY ==="] = "issueSummary.header",
        [BackendConsoleMilestonePatterns.ExtractionCompleteHeader] = "consoleMessages.extractionCompleteHeader",
        [BackendConsoleMilestonePatterns.FinalProcessingCompleted] = "consoleMessages.finalProcessingCompleted",
        [BackendConsoleMilestonePatterns.AssetExtractionSuccess] = "consoleMessages.successExtractionComplete",
        [BackendConsoleMilestonePatterns.ExtractionCompletedSuccessfully] = "consoleMessages.extractionCompletedSuccessfully",
        [BackendConsoleMilestonePatterns.RdaDecompressionSuccessful] = "consoleMessages.rdaDecompressionSuccessful",
        [BackendConsoleMilestonePatterns.RdaDecompressionSuccessfulAvailable] = "consoleMessages.rdaDecompressionSuccessful",
        [BackendConsoleMilestonePatterns.ExtractionCompleteHeaderTagged] = "consoleMessages.extractionCompleteHeader",
        [BackendConsoleMilestonePatterns.ProcessedZeroAssetsInfoTagged] = "consoleMessages.processedZeroAssetsSummary",
        [BackendConsoleMilestonePatterns.ProcessedZeroResourcesInfoTagged] = "consoleMessages.processedZeroAssetsSummary",
        [BackendConsoleMilestonePatterns.AssetExtractionSuccessTagged] = "consoleMessages.successExtractionComplete",
        [BackendConsoleMilestonePatterns.NoTextsEnglishInArchive] = "consoleMessages.noTextsEnglishInArchive",
        ["RDA decompression successful - game data now accessible as XML"] = "consoleMessages.rdaDecompressionSuccessful",
        ["RDA decompression successful - game data now available as XML"] = "consoleMessages.rdaDecompressionSuccessful",
        ["✓ Asset extraction completed successfully!"] = "consoleMessages.successExtractionComplete",
        ["Extraction completed successfully!"] = "consoleMessages.extractionCompletedSuccessfully",
        ["[OUTPUT CACHE] Force full rebuild requested."] = "consoleMessages.outputCacheForceFullRebuild",
        ["[OUTPUT CACHE] Game build changed but extractable assets are unchanged — skipping rebuild."] = "consoleMessages.outputCacheGameDiffNone",
        ["[OUTPUT CACHE] Source XML tree changed but extractable assets match inventory — refreshing stamp."] = "consoleMessages.outputCacheSourceAccepted",
        ["[OUTPUT CACHE] Inventory missing source hashes — backfilling from source_xml (no full wipe)."] = "consoleMessages.outputCacheHashBackfill",
    };

    private static readonly Regex FormattingRunSummaryRegex = new(
        @"^\[INFO\] Formatting summary: ([\d,]+) files annotated \(([\d,]+) comments\), ([\d,]+) template moves, ([\d,]+) missing Template node$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheCompleteRegex = new(
        @"^\[OUTPUT CACHE\] Output is complete — skipping rebuild \(build: (.+), ([\d,]+) assets\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheRepairRegex = new(
        @"^\[OUTPUT CACHE\] Repairing output — ([\d,]+) asset\(s\) missing, incomplete, or content-changed\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheFullRebuildRegex = new(
        @"^\[OUTPUT CACHE\] Full rebuild \((.+)\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheGameDiffRegex = new(
        @"^\[OUTPUT CACHE\] Game update diff — \+([\d,]+) ~([\d,]+) -([\d,]+) \(rebuild ([\d,]+)\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheSourceDiffRegex = new(
        @"^\[OUTPUT CACHE\] Source XML changed — diff repair \+([\d,]+) ~([\d,]+) -([\d,]+) \(rebuild ([\d,]+)\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex OutputCacheHashBackfillDiffRegex = new(
        @"^\[OUTPUT CACHE\] Inventory missing source hashes — DiffRepair after backfill check \(added ([\d,]+), changed ([\d,]+), removed ([\d,]+), rebuild ([\d,]+)\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SourceXmlCacheCorruptRegex = new(
        @"^\[SOURCE CACHE\] source_xml required file failed integrity \((.+)\) — re-extracting from RDA \(build: (.+)\)\.$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string prefix, string suffix, string keyName)[] ParameterizedBackendPatterns =
    [
        ("reading language file (", ")...", "consoleMessages.readingLanguageFileWithLang"),
        ("Extracting ", " assets to XML...", "consoleMessages.extractingToXml"),
        ("Inheriting template properties for ", " assets...", "consoleMessages.inheritingTemplateProperties"),
        ("Preparing final formatting pass - scanning output directory...", " files found", "consoleMessages.preparingFormattingWithCount"),
        ("Final processing of ", " files...", "consoleMessages.finalProcessing"),
        ("Processing ", " BaseAssetGUID reference files...", "consoleMessages.processingBaseAssetGuid")
    ];

    private static readonly (string search, string resourceKey)[] ProgressReplacements =
    [
        (" - Extracting: ", "consoleMessages.extractingLabel"),
        (" - Extracting from RDA: ", "consoleMessages.extractingFromRdaLabel"),
        (" - Merging: ", "consoleMessages.mergingLabel"),
        (" - Resolving: ", "consoleMessages.resolvingLabel"),
        (" - Scanning: ", "consoleMessages.scanningLabel"),
        (" - Processing: ", "consoleMessages.processingProgress"),
        (" - Formatting references: ", "consoleMessages.formattingReferencesProgress"),
        (" - Indexing: ", "consoleMessages.indexingLabel"),
        (" - Inheriting: ", "consoleMessages.inheritingLabel"),
        (" - Annotating: ", "consoleMessages.annotatingLabel"),
        (" - Hashing: ", "consoleMessages.hashingLabel"),
        (" - Creating mod: ", "consoleMessages.creatingModLabel")
    ];

    private static readonly (string search, string resourceKey)[] ProgressSuffixReplacements =
    [
        (" - Formatting....", "consoleMessages.formattingProgress"),
        (" - Formatting...", "consoleMessages.formattingProgress"),
        (" - Merging...", "consoleMessages.mergingProgress"),
        (" - Extracting...", "consoleMessages.extractingProgress")
    ];

    private static readonly (string search, string resourceKey)[] ProgressSummaryReplacements =
    [
        (" - Formatting assets", "consoleMessages.formattingAssets"),
        (" - Extracting assets", "consoleMessages.extractingAssets"),
        (" - Merging templates", "consoleMessages.mergeTemplates"),
        (" - Resolving dependencies", "consoleMessages.resolvingDependenciesSummary"),
        (" - Scanning dependencies", "consoleMessages.scanningDependenciesSummary"),
        ("Extracting from RDA", "consoleMessages.extractingFromRda"),
        ("Building GUID index", "consoleMessages.buildingGuidIndex"),
        ("Annotating template comments", "consoleMessages.annotatingTemplateComments"),
        ("Creating asset mods", "consoleMessages.creatingAssetMods")
    ];

    private static readonly (string match, string keyName)[] AutoUpdatePatterns =
    [
        ("Game templates changed - updating configuration", "consoleMessages.autoUpdateUpdating"),
        ("Skipped - template list appears custom", "consoleMessages.autoUpdateSkipped")
    ];

    private static readonly (string marker, string suffix, string keyName)[] AutoUpdateCountPatterns =
    [
        ("Updated template config with ", " templates", "consoleMessages.autoUpdateConfigUpdated"),
        ("Updated template list with ", " templates from game", "consoleMessages.autoUpdateListUpdated")
    ];
}
