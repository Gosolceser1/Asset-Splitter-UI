using System.Text.Json;
using AssetProcessor;

namespace AssetProcessor;

/// <summary>
/// Provides localized console messages loaded from user-editable JSON files.
/// Files are located in <c>config/05_Console_Messages/console_{language}.json</c>.
/// Falls back to embedded English defaults when the file is not found.
/// </summary>
public static class ConsoleMessages
{
    private const string EnglishLanguage = "en";
    private const string ConsoleMessagesFolder = "05_Console_Messages";

    private static string _currentLanguage = EnglishLanguage;
    private static Dictionary<string, string> _messages = [];
    private static Dictionary<string, string> _fallbackMessages = [];
    private static bool _initialized;

    private static readonly Dictionary<string, string> DefaultMessages = new()
    {
        ["extractingFromRda"] = "extracting from RDA file(s)...",
        ["buildingGuidIndex"] = "Building GUID index...",
        ["readingLanguageFile"] = "reading language file",
        ["readingPropertiesFile"] = "reading properties file...",
        ["extractingAssets"] = "extracting assets...",
        ["extractingProgress"] = "Extracting...",
        ["mergingProgress"] = "Merging...",
        ["mergeTemplates"] = "Merging templates...",
        ["formattingAssets"] = "formatting assets...",
        ["formattingProgress"] = "Formatting...",
        ["preparingFormatting"] = "Preparing final formatting pass - scanning output directory...",
        ["completedLabel"] = "completed",
        ["done"] = "done.",
        ["doneLabel"] = "[DONE]",
        ["dependenciesResolved"] = "dependencies resolved",
        ["extractionComplete"] = "Extraction complete",
        ["fixLabel"] = "[FIX]",
        ["resolvingDependencies"] = "Resolving BaseAssetGUID dependencies for",
        ["assetsLabel"] = "assets...",
        ["continuingWithoutTranslations"] = "Continuing without translations...",
        ["rdaExtractionComplete"] = "[RDA COMPLETE] ✓ Game files extracted. Configure options in the GUI, then run again for asset processing",
        ["rdaExtractionSuccess"] = "[SUCCESS] All language files extracted successfully!",
        ["rdaExtractionInfo"] = "[INFO] Configure your options in the GUI (language and processing features), then run again for asset processing",
        ["phase1Label"] = "=== PHASE 1: RDA EXPLORER ===",
        ["phase2Label"] = "=== PHASE 2: ASSET SPLITTER ===",
        ["phase2bLabel"] = "=== PHASE 2B: TEMPLATE PROPERTY INHERITANCE ===",
        ["phase3Label"] = "=== PHASE 3: ASSET EXTRACTION ===",
        ["phase3SplitTemplatesLabel"] = "=== PHASE 3B: SPLIT TEMPLATES ===",
        ["phase4GuidIndexLabel"] = "=== PHASE 4: GUID FILE INDEX ===",
        ["phase5TemplateMergeLabel"] = "=== PHASE 5: TEMPLATE INHERITANCE (MERGE) ===",
        ["phase6DependencyResolutionLabel"] = "=== PHASE 6: DEPENDENCY RESOLUTION ===",
        ["phase7FormattingLabel"] = "=== PHASE 7: FORMATTING ===",
        ["bannerTitle"] = "│  ANNO ASSET SPLITTER v2.0 Enhanced Edition     │",
        ["bannerAnno1800"] = "│  🎮 Anno 1800  ............ ✓ Supported        │",
        ["bannerAnno117"] = "│  🎮 Anno 117 (Pax) ....... ✓ Supported        │",
        ["readyToExtract"] = "│  Status: Ready to Extract                      │",
        ["gameDetectedPrefix"] = "│  ✓ Detected Game: {0}",
        ["gameNotDetected"] = "[ERROR] GAME NOT DETECTED",
        ["assetSplitVersion"] = "AssetSplit v1.0 - Based on Pogobuckel's work",
        ["enhancedEdition"] = "Enhanced Edition 2022-2026",
        ["syntaxLabel"] = "Syntax:",
        ["helpCommand"] = "AssetSplit -h for help",
        ["permissionsError"] = "Please check permissions and disk space.",
        ["outputFolderNotEmpty"] = "Output folder is not empty. Exiting...",
        ["sourceFilesCopied"] = "Source files copied to {0}",
        ["xmlFileNotFound"] = "XML file '{0}' not found.",
        ["errorLoadingDocuments"] = "Error: Could not load template or property documents",
        ["fixerErrorPrefix"] = "Fixer error",
        ["helpUsage"] = "USAGE:",
        ["helpRequiredParameters"] = "REQUIRED PARAMETERS:",
        ["helpOptions"] = "OPTIONS:",
        ["helpExamples"] = "EXAMPLES:",
        ["helpOutputStructure"] = "OUTPUT STRUCTURE:",
        ["helpShortSyntax"] = "AssetSplit <source> <out> <lang> [-c] [-f] [-t] [-d] [-u:file] [-x:file] [-g:GUID] [--create-asset-mods]",
        ["helpBannerTitle"] = "║                    AssetSplit v1.0 - Anno 1800 Asset Processor               ║",
        ["helpBannerCredit"] = "║                     Based on original work by Pogobuckel                     ║",
        ["helpBannerEdition"] = "║                        Enhanced version - © 2022-2026                        ║",
        ["helpSyntaxLine"] = "  AssetSplit <source> <output> <language> [options]",
        ["helpParamSource"] = "  <source>      Anno 1800 installation directory",
        ["helpParamOutput"] = "  <output>      Target directory for extracted assets",
        ["helpParamLanguage"] = "  <language>    Localization: english, french, german, italian, polish, spanish, russian, chinese, japanese, korean",
        ["helpOptionComments"] = "  -c            Add translated GUID comments to XML files",
        ["helpOptionDependencies"] = "  -f            Resolve BaseAssetGUID dependencies (complete assets)",
        ["helpOptionTemplates"] = "  -t            Organize files into template-based folders",
        ["helpOptionDebug"] = "  -d            Enable debug output for troubleshooting",
        ["helpOptionOverwrite"] = "  -y            Overwrite existing output directory",
        ["helpOptionCustomTemplates"] = "  -u [file]     Use custom template list (default: used_templates.txt)",
        ["helpOptionCustomFixlist"] = "  -x [file]     Use custom fixlist file (default: game-specific built-in)",
        ["helpOptionSingleGuid"] = "  -g [GUID]     Extract single asset by GUID",
        ["helpOptionNoModOpsWrap"] = "  --no-modops-wrap    Output raw Asset XML without ModOps/ModOp wrapper",
        ["helpOptionNoDefaultProperties"] = "  --no-default-properties  Do not apply default properties from properties.xml",
        ["helpOptionSplitTemplates"] = "  --split-templates   Split templates.xml into per-template files in template-named folders",
        ["helpOptionCreateAssetMods"] = "  --create-asset-mods Create one ready-to-copy Mod Loader folder per generated asset XML",
        ["helpOptionAutoTemplates"] = "  --auto-templates    Auto-extract all templates from game (ignores used_templates.txt)",
        ["helpOptionUpdateTemplates"] = "  --update-templates  Update template list from game's templates.xml",
        ["helpOptionCompareTemplates"] = "  --compare-templates Compare config templates with game templates",
        ["helpExampleBasic"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" extracted_assets english",
        ["helpExampleFull"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" my_mods german -c -f -t",
        ["helpExampleSingle"] = "    AssetSplit \"C:\\Program Files\\Ubisoft\\Ubisoft Game Launcher\\games\\Anno 1800\" test_output english -g 1010017",
        ["helpOutputXml"] = "  output_xml/           XML files for modding",
        ["helpSourceXml"] = "  source_xml/           Raw extracted data from Anno 1800",
        ["failedToLoadAssetNames"] = "Failed to load asset names: {0}",
        ["templateXmlNotFoundInPath"] = "[ERROR] Could not find templates.xml in {0}",
        ["templateXmlNotFound"] = "[ERROR] Could not find templates.xml",
        ["noTemplatesFound"] = "[ERROR] No templates found in templates.xml",
        ["templateExtractionFailed"] = "[ERROR] Template extraction failed: {0}",
        ["templateParseFailed"] = "[ERROR] Failed to parse templates.xml: {0}",
        ["templateComparisonFailed"] = "[ERROR] Template comparison failed: {0}",
        ["templateWriteFailed"] = "[ERROR] Failed to write templates to file: {0}",
        ["templateUpdateSucceeded"] = "[OK] Successfully updated template list with {0} templates",
        ["templateUpdateFailed"] = "[ERROR] Failed to update templates",
        ["templateFileEmpty"] = "[WARN] Template file is empty or contains only comments: {0}",
        ["noAssetsExtractedUpdateTemplates"] = "[INFO] No assets will be extracted - run with --update-templates to populate from game",
        ["failedToLoadTemplatesFrom"] = "[ERROR] Failed to load templates from {0}: {1}",
        ["compareTemplatesInGame"] = "[COMPARE] Templates in game:   {0}",
        ["compareTemplatesInConfig"] = "[COMPARE] Templates in config: {0}",
        ["compareTemplatesUnchanged"] = "[COMPARE] Unchanged:           {0}",
        ["compareTemplatesNew"] = "[COMPARE] New in game:         {0}",
        ["compareTemplatesRemoved"] = "[COMPARE] Removed from game:   {0}",
        ["compareTemplatesNewHeader"] = "[NEW] Templates in game but not in config:",
        ["compareTemplatesRemovedHeader"] = "[REMOVED] Templates in config but not in game:",
        ["compareTemplatesMore"] = "      ... and {0} more",
        ["templatesInSync"] = "[OK] Templates are in sync! ✓",
        ["basicExtraction"] = "Basic extraction:",
        ["fullExtraction"] = "Full extraction with all features:",
        ["singleAsset"] = "Extract a single asset for testing:",
        ["bannerSubtitle"] = "│  Asset Extraction & ModOp Generator            │",
        ["extractingGameData"] = "Extracting game data from RDA archives...",
        ["buildingAssetRegistry"] = "Building asset name registry from game database...",
        ["preparingBaseAssetGuid"] = "Preparing BaseAssetGUID reference pass...",
        ["processingBaseAssetGuid"] = "Processing {0} BaseAssetGUID reference files...",
        ["resolvingDependenciesSummary"] = "Resolving dependencies",
        ["scanningDependenciesSummary"] = "Scanning dependencies",
        ["extractingToModOp"] = "Extracting {0} assets to ModOp format...",
        ["extractingToXml"] = "Extracting {0} assets to XML...",
        ["resolvingDepsFor"] = "Resolving BaseAssetGUID dependencies for {0} assets...",
        ["extractingFromRdaCompressed"] = "Extracting game data from compressed RDA archive files...",
        ["singleGuidNotFound"] = "[WARNING] GUID '{0}' not found in game assets. Check the GUID or run full extraction first.",
        ["extractFormatModOp"] = "to installable ModOp format",
        ["extractFormatXml"] = "to XML (raw Asset)",
        ["extractConvertingAssets"] = "[EXTRACT] Converting {0} assets {1}...",
        ["extractCreatingXmlMods"] = "[INFO] Creating XML mod files that Anno can load and apply to the game",
        ["extractAssetFailed"] = "[ERROR] Failed to extract asset {0}: {1}",
        ["splittingTemplates"] = "Splitting templates.xml into output_templates_{0}...",
        ["splitTemplatesComplete"] = "Created {0} template files in folder: {1}",
        ["annotatingTemplateComments"] = "Adding GUID comments to template files...",
        ["phase8AssetModPackages"] = "=== PHASE 8: ASSET MOD PACKAGES ===",
        ["assetRegistryBuilding"] = "[ASSETS] Building asset registry from game's master asset database...",
        ["assetRegistryCataloging"] = "[INFO] Cataloging all buildings, products, and items in the game",
        ["assetRegistryComplete"] = "[COMPLETE] Asset registry: {0} game assets cataloged and ready",
        ["assetRegistryInherited"] = "[INFO] Inherited {0} names from parent assets",
        ["audioGeneratedMissing"] = "[INFO] audio_generated.xml not found - skipping audio asset names",
        ["audioRegistryComplete"] = "[COMPLETE] Audio registry: {0} audio assets cataloged",
        ["audioGeneratedLoadFailed"] = "[WARNING] Failed to load audio_generated.xml: {0}",
        ["templateSplitMissingTemplatesXml"] = "[SKIP] templates.xml not found - skipping template split",
        ["templateSplitNoTemplateNodes"] = "[SKIP] No Template nodes in templates.xml",
        ["okWithMessage"] = "[OK] {0}",
        ["creatingOutputDirectory"] = "[INFO] Creating output directory: {0}",
        ["outputDirectoryCreated"] = "[OK] Output directory created successfully",
        ["outputDirectoryCreateFailed"] = "[ERROR] Failed to create output directory '{0}': {1}",
        ["clearedExistingOutputFolder"] = "[INFO] Cleared existing output folder",
        ["outputFolderClearFailed"] = "[ERROR] Failed to clear output folder: {0}",
        ["gameDirectoryNotFound"] = "Game directory not found: {0}",
        ["solutionsLabel"] = "Solutions:",
        ["solutionVerifyGamePath"] = "  1. Verify Anno game installation path",
        ["solutionUseFullPath"] = "  2. Use full path to Anno installation directory",
        ["solutionRunLauncher"] = "  3. Run via launcher script for automatic detection",
        ["mergingInheritedProperties"] = "[FIX] Merging inherited properties from parent assets",
        ["guidIndexNull"] = "GuidIndex is null - cannot resolve dependencies.",
        ["dependenciesResolvedComplete"] = "[COMPLETE] ✓ Resolved {0} dependencies",
        ["resolvingAssetProgress"] = "Resolving: {0} <- {1}",
        ["scanningAssetProgress"] = "Scanning: {0}",
        ["formatFeatureVectorCleanup"] = "VectorElement cleanup",
        ["formatFeatureRegionalIngredients"] = ", Regional ingredients",
        ["formatFeatureCommentsFolders"] = ", Comments (-c), Folders (-t)",
        ["formatProcessingFiles"] = "[FORMAT] Processing {0} files - {1}",
        ["finalProcessingFiles"] = "Final processing of {0} files...",
        ["applyingXmlCleanup"] = "Applying: XML cleanup",
        ["applyingRegionalIngredients"] = ", Regional ingredients",
        ["applyingTranslations"] = ", Translations",
        ["applyingFolderOrganization"] = ", Folder organization",
        ["finalProcessingCompleted"] = "[COMPLETE] Final processing completed:",
        ["vectorCleanupApplied"] = "  * VectorElement cleanup: Applied to all {0} assets",
        ["regionalIngredientsApplied"] = "  * Regional ingredients: Applied (Anno 1800 Africa DLC)",
        ["translationCommentsSummary"] = "  * Translation comments: {0}",
        ["templateOrganizationSummary"] = "  * Template organization: {0}",
        ["addedCFlag"] = "Added (-c flag)",
        ["skippedNoCFlag"] = "Skipped (no -c flag)",
        ["appliedTFlag"] = "Applied (-t flag)",
        ["skippedNoTFlag"] = "Skipped (no -t flag)",
        ["processingAssetProgress"] = "Processing: {0}",
        ["failedToLoadXmlFile"] = "[ERROR] Failed to load XML file: {0}",
        ["unexpectedFileProcessingError"] = "[ERROR] Unexpected error processing file: {0}",
        ["formatSingleFileFailed"] = "FormatSingleFile failed for {0}: {1}",
        ["couldNotMoveToTemplateFolder"] = "[WARN] Could not move file to template folder: {0}",
        ["parentItemNull"] = "[WARN] Parent item at index {0} is null",
        ["propertyScanAnalyze"] = "[ANALYZE] Scanning properties.xml to identify comment-eligible XML elements...",
        ["propertyScanInfo"] = "[INFO] Finding properties with value '0' that can receive translated GUID comments",
        ["propertyScanComplete"] = "[COMPLETE] Property analysis: {0} XML elements can receive translated comments",
        ["translationMappingsLoading"] = "[TRANS] Loading GUID-to-text translation mappings ({0})...",
        ["translationDictionaryBuilding"] = "[INFO] Building dictionary to convert numeric GUIDs into readable names",
        ["translationFileLoadFailed"] = "[ERROR] Failed to load translation file '{0}': {1}",
        ["translationDictionaryComplete"] = "[COMPLETE] Translation dictionary: {0} GUID to name mappings ready",
        ["filesFound"] = "{0} files found",
        ["formatProcessingMainOutputFiles"] = "[FORMAT] Processing {0} main output files",
        ["formatProcessingBaseAssetGuidFilesNoMove"] = "[FORMAT] Processing {0} BaseAssetGUID reference files (no folder move)",
        ["assetExtractionSuccess"] = "[SUCCESS] ✓ Asset extraction completed successfully!",
        ["mergeCheckingTemplateInheritance"] = "[MERGE] Checking {0} assets for template property inheritance...",
        ["mergeOnlyFixlistTemplates"] = "[INFO] Only fixlist templates get complete properties merged",
        ["inheritingTemplateProperties"] = "Inheriting template properties for {0} assets...",
        ["mergingAssetProgress"] = "Merging: {0}",
        ["mergeAssetFailed"] = "[ERROR] Failed to merge {0}: {1}",
        ["templateInheritanceComplete"] = "[COMPLETE] Template inheritance: {0}/{1} assets got complete properties",
        ["templateNotFoundInCache"] = "[WARN] Template '{0}' not found in cache - this should not happen",
        ["templatesCheckingUpdate"] = "[TEMPLATES] Checking if template configuration needs update...",
        ["rdaAnno117Detected"] = "[RDA] Anno 117 detected - decompressing assets.xml, templates.xml, properties.xml, properties-meta.xml, datasets.xml, audio_generated.xml...",
        ["rdaExtractingAnno117CoreFiles"] = "[INFO] Extracting core game definition files from Anno 117 RDA archives",
        ["rdaAnno1800Detected"] = "[RDA] Anno 1800 detected - decompressing assets.xml, templates.xml, properties-toolone.xml, datasets.xml...",
        ["rdaExtractingAnno1800CoreFiles"] = "[INFO] Extracting core game definition files from Anno 1800 RDA archives",
        ["rdaFilesContainAssets"] = "RDA files are container archives that store Anno game assets (some blocks/files may be compressed or encrypted)",
        ["rdaDecompressionSuccessful"] = "[COMPLETE] RDA decompression successful - game data now accessible as XML",
        ["loadingGameTemplatesConfig"] = "[CONFIG] Loading game templates and processing configurations...",
        ["convertingRawXmlToMods"] = "[INFO] Converting raw XML game data into installable Anno mod files",
        ["configurationLoadedReady"] = "[READY] Configuration loaded - beginning asset conversion...",
        ["rdaExtractionFailed"] = "[ERROR] RDA extraction failed: {0}",
        ["couldNotCopyToSourceXml"] = "[WARN] Could not copy {0} to source_xml: {1}",
        ["rdaCompleteSelectLanguage"] = "[RDA COMPLETE] Game files extracted - Select language and run again for asset processing",
        ["assetsXmlDocumentElementLoadFailed"] = "[ERROR] Could not load assets.xml document element",
        ["singleGuidAsset"] = "[SINGLE GUID] Asset: {0} - {1}",
        ["singleGuidXmlOutput"] = "[SINGLE GUID XML] Output: {0}",
        ["autoUpdateUpdating"] = "[AUTO-UPDATE] Game templates changed - updating configuration...",
        ["autoUpdateConfigUpdated"] = "[AUTO-UPDATE] ✓ Updated template config with {0} templates",
        ["autoUpdateListUpdated"] = "[AUTO-UPDATE] ✓ Updated template list with {0} templates from game",
        ["autoUpdateSkipped"] = "[AUTO-UPDATE] Skipped - template list appears custom. Use --update-templates to overwrite.",
        ["autoUpdateReturnedZero"] = "[WARN] Template auto-update returned 0 templates, using existing config",
        ["templatesAlreadyUpToDate"] = "[TEMPLATES] Configuration already up-to-date with game",
        ["autoUpdateFailed"] = "[WARN] Template auto-update failed: {0}",
        ["continuingExistingTemplateConfig"] = "[INFO] Continuing with existing template configuration",
        ["autoTemplatesExtractingAll"] = "[AUTO-TEMPLATES] Extracting all templates from game files...",
        ["autoTemplatesFound"] = "[AUTO-TEMPLATES] Found {0} templates in game",
        ["autoTemplatesNoneFound"] = "[WARNING] Auto-template extraction found no templates, falling back to config file",
        ["configReadyWithTemplates"] = "[CONFIG] Ready with {0} templates",
        ["failedToLoadTemplates"] = "[ERROR] Failed to load templates: {0}",
        ["assetModsCreatingSingle"] = "[SINGLE GUID MOD] Creating Mod Loader mod at: {0}",
        ["assetModsCreatingMany"] = "[MODS] Creating one Mod Loader folder per asset in: {0}",
        ["assetModsProgress"] = "Creating asset mods",
        ["assetModsCreatedSingle"] = "[SINGLE GUID MOD] Mod folder created at: {0}",
        ["assetModsSingleXmlOutput"] = "[SINGLE GUID XML] Asset XML output at: {0}",
        ["assetModsCreatedSummary"] = "[MODS] Created {0} asset mod folders. Skipped {1}.",
        ["assetModsOutputFolder"] = "[MODS] Output folder: {0}",
        ["assetModsSkippingInvalidXml"] = "[WARN] Skipping asset mod package for invalid XML: {0}",
        ["assetModsReadXmlWarning"] = "[WARN] Could not read asset XML '{0}': {1}",
        ["debugWorkPlanAnnounced"] = "[DEBUG][PLAN] Work-unit budget for UI progress: {0} (main XML files: {1}; includes GUID index {2}, merge {3}, dependencies {4}, formatting {5}, asset mods {6})",
        ["debugTemplateMergeStats"] = "[DEBUG][MERGE] Template inheritance: {0} merged from fixlist, {1} kept basic (not in fixlist)",
        ["debugParentCacheStats"] = "[DEBUG][CACHE] Parent GUID cache: {0} loaded from disk, {1} reused from cache",
        ["debugFormattingFileStats"] = "[DEBUG][FORMAT] Per-file work: {0} files annotated ({1} comments), {2} moved to template folders, {3} missing Template node",
        ["debugModExportSource"] = "[DEBUG][MODS] Source XML folder: {0}",
        ["debugModExportTarget"] = "[DEBUG][MODS] Mod packages output: {0}",
        ["debugPhaseGuidIndex"] = "[DEBUG][PHASE] Building GUID file index (maps each asset GUID to its XML path for merge/dependency resolution)",
        ["debugGuidIndexComplete"] = "[DEBUG][PHASE] GUID index ready in {0} — {1} files indexed",
        ["debugTemplateSplitFile"] = "[DEBUG][SPLIT] Wrote template file: {0}.xml",
        ["debugTemplateSplitSummary"] = "[DEBUG][SPLIT] Split {0} templates from {1} into {2} ({3} template files written)",
        ["debugAssetModsProcessingFiles"] = "[DEBUG] Processing {0} files into asset mod packages",
        ["debugAssetModsCreatingPackage"] = "[DEBUG] Creating mod package for: {0}",
        ["debugAssetModsSkippedInvalidMetadata"] = "[DEBUG] Skipped {0} - invalid or missing metadata",
        ["debugAssetModsPackageCreated"] = "[DEBUG] Mod package created: {0} -> GUID {1} ({2})",
        ["debugModPackageStats"] = "[DEBUG][MODS] Built {0} mod packages ({1} skipped)",
        ["debugTotalArguments"] = "[DEBUG] Total arguments: {0}",
        ["debugFlagsApplied"] = "[DEBUG] Flags applied: -c(comments)={0} -f(BaseAssetGUID)={1} -t(template folders)={2} --modops-wrap={3} --split-templates={4} --create-asset-mods={5}",
        ["debugSingleGuidFilter"] = "[DEBUG] singleGuidFilter='{0}' length={1}",
        ["debugRegistryIncludesAssets"] = "[DEBUG] Registry includes all buildings, products, items, and game objects",
        ["debugPropertiesWillGetComments"] = "[DEBUG] These properties will get <!-- comments --> when -c flag is used",
        ["debugTranslationExamples"] = "[DEBUG] Examples: GUID '1010017' -> 'Coins', '1010196' -> 'Timber'",
        ["debugStep3AddingComments"] = "[DEBUG] STEP 3: Adding translated comments to XML elements (-c flag enabled)",
        ["debugStep3SkippingComments"] = "[DEBUG] STEP 3: Skipping comments - use -c flag to enable translated comments",
        ["debugStep4OrganizingFolders"] = "[DEBUG] STEP 4: Organizing files into template folders (-t flag enabled)",
        ["debugStep4KeepingMainDirectory"] = "[DEBUG] STEP 4: Keeping files in main directory - use -t flag for template folders",
        ["debugXmlLoadError"] = "[DEBUG] XML load error: {0}",
        ["debugVectorElementsRemoved"] = "[DEBUG] Removed {0} VectorElement nodes from XML structure",
        ["debugNoTemplateNodeFound"] = "[DEBUG] No Template node found in {0} - continuing anyway",
        ["debugTranslatedCommentsAdded"] = "[DEBUG] Added {0} translated comments to XML elements",
        ["regionAfrican"] = "African",
        ["regionDefaultEuropean"] = "Default/European",
        ["debugBuildingRegionDetected"] = "[DEBUG] Building region detected: {0} ({1})",
        ["debugAppliedIngredients"] = "[DEBUG] Applied {0} {1} ingredients to building costs",
        ["debugStep4MovingFileToTemplateFolder"] = "[DEBUG] STEP 4: Moving file to template folder '{0}' (-t flag enabled)",
        ["debugTemplateSkippedNotInFixlist"] = "[DEBUG] SKIPPING: Template '{0}' not in fixlist - keeping basic asset only",
        ["debugTemplateMergingInFixlist"] = "[DEBUG] MERGING: Template '{0}' is in fixlist - inheriting complete properties",
        ["debugCacheBuildingTemplateProperties"] = "[CACHE] Building template property cache for {0} templates...",
        ["debugCacheTemplateFailed"] = "[WARN] Failed to cache template '{0}': {1}",
        ["debugCacheTemplatePropertiesComplete"] = "[CACHE] Cached {0} template property trees (saves ~{1}ms in XPath lookups)",
        ["debugDependencyCacheInitialized"] = "[FIX] Initialized caches for dependency resolution - ready for Phase 5 (GUID index: {0} entries)",
        ["debugParentAssetLoadedFromCache"] = "[CACHE] Parent asset {0} loaded from cache (hit)",
        ["debugParentAssetNotFound"] = "[WARN] Parent asset {0} not found",
        ["debugParentAssetLoadedFromDisk"] = "[CACHE] Parent asset {0} loaded from disk and cached",
        ["debugParentAssetLoadFailed"] = "[ERROR] Failed to load parent asset {0}: {1}",
        ["debugDependencyCacheCleared"] = "[COMPLETE] Cleared dependency resolution caches - freed memory",
        ["debugArgumentValue"] = "[DEBUG] args[{0}] = '{1}'",
        ["debugStartupFromGui"] = "[DEBUG][START] game='{0}' | output='{1}' | assetTexts={2} | consoleLang={3} | flags: {4}",
        ["debugFlowStep"] = "[DEBUG][FLOW] {0}",
        ["debugExtractedGuid"] = "[DEBUG] Extracted GUID: '{0}'",
        ["debugCleanSingleGuidOutputFailed"] = "[DEBUG] Could not clean empty single GUID output folder: {0}",
        ["debugReadAssetNameFromOutputFailed"] = "[DEBUG] Could not read asset name from output: {0}",
        ["debugSingleGuidModeActive"] = "[DEBUG] SINGLE GUID MODE ACTIVE",
        ["debugParseAssetFailed"] = "[DEBUG] Failed to parse asset: {0}",
        ["debugInheritedIndexProcessingError"] = "[DEBUG] Error processing InheritedIndex: {0}",
        ["debugElementTranslation"] = "[DEBUG] Element: <{0}>{1}</{0}> -> Translation: '{2}' (length={3})",
        ["debugInvalidCommentText"] = "[DEBUG] Invalid comment text: '{0}' - {1}",
        ["debugCommentInsertionError"] = "[DEBUG] Comment insertion error: {0} - {1}",
        ["debugAnnotateFileCommentsError"] = "[DEBUG] AnnotateFilesWithGuidComments error on {0}: {1}",

        // RDA Explorer — full developer trace
        ["debugRdaGameRoot"] = "[DEBUG][RDA] Game installation root: {0}",
        ["debugRdaMainDataFolder"] = "[DEBUG][RDA] Searching maindata folder: {0}",
        ["debugRdaOutputFolder"] = "[DEBUG][RDA] Output folder (extract target): {0}",
        ["debugRdaBareExtractMode"] = "[DEBUG][RDA] Extract mode: bare={0} (files written flat into output folder by filename)",
        ["debugRdaAssetLanguage"] = "[DEBUG][RDA] Language requirement for this run: {0}",
        ["debugRdaFilterCriteria"] = "[DEBUG][RDA] Filter criteria ({0} OR-groups): {1}",
        ["debugRdaFilterGroup"] = "[DEBUG][RDA]   Group {0}: match when {1}",
        ["debugRdaArchiveSearchOrder"] = "[DEBUG][RDA] Archive search order: {0}",
        ["debugRdaArchiveSearchOrderAnno1800"] = "[DEBUG][RDA] Archive search order: data*.rda highest index first (stop when core files + language found)",
        ["debugRdaProcessingOrder"] = "[DEBUG][RDA] Processing order this run: {0}",
        ["debugRdaArchivePath"] = "[DEBUG][RDA] Opening archive: {0}",
        ["debugRdaArchiveInternals"] = "[DEBUG][RDA] Archive internals: format={0}, parsedBlocks={1}, totalEntries={2}, entryFlags(compressed={3}, encrypted={4}, memory-resident={5}, deleted={6})",
        ["debugRdaArchiveLayout"] = "[DEBUG][RDA] Archive layout: fileBytes={0}, firstBlockOffset={1}, parsedBlockHeaders={2}",
        ["debugRdaEntryFlagScope"] = "[DEBUG][RDA] Note: entryFlags are per-file flags from block metadata (not a statement that the whole .rda file itself is or is not compressed).",
        ["debugRdaBlockDetail"] = "[DEBUG][RDA]   Block #{0}: offset={1}, flags={2} [compressed={3}, encrypted={4}, memory-resident={5}, deleted={6}], files={7}, dirSize={8}, dirSizeDecoded={9}, nextBlock={10}",
        ["debugRdaMissingArchive"] = "[DEBUG][RDA] Archive not found (skipped): {0}",
        ["debugRdaExtractedFile"] = "[DEBUG][RDA] Extracted: {0} -> {1} ({2} bytes)",
        ["debugRdaSkippedEntry"] = "[DEBUG][RDA] Skipped: {0} — {1}",
        ["debugRdaArchiveSummary"] = "[DEBUG][RDA] Archive '{0}': {1} entries scanned, {2} extracted, {3} skipped checksum/metadata, {4} skipped invalid path, {5} no filter match",
        ["debugRdaEarlyStopReason"] = "[DEBUG][RDA] Early stop — all required files present: {0}",
        ["debugRdaContinueSearch"] = "[DEBUG][RDA] Core files still missing after {0} — trying next archive",
        ["debugRdaOutputInventory"] = "[DEBUG][RDA] Output folder now has {0} file(s) in {1}:",
        ["debugRdaOutputFile"] = "[DEBUG][RDA]   • {0} ({1} bytes)",
        ["debugRdaOutputMissing"] = "[DEBUG][RDA] Output folder does not exist after extraction: {0}",
        ["debugRdaStartingPhase"] = "[DEBUG][RDA] Starting {0} RDA extraction phase. Found {1} archives.",
        ["debugRdaSkippingMissing"] = "[DEBUG][RDA] Skipping missing archive: {0}",
        ["debugRdaProcessing"] = "[DEBUG][RDA] Processing {0} ({1}/{2})",
        ["debugRdaFoundCoreFiles"] = "[DEBUG][RDA] Found all required core files after {0}. Stopping early.",
        ["debugRdaFinishedPhase"] = "[DEBUG][RDA] Finished {0} RDA extraction phase.",

        ["debugGuidStartingBuild"] = "[DEBUG][GUID] Starting GUID index build. Total files to scan: {0}",
        ["debugGuidScanRoot"] = "[DEBUG][GUID] Scanning XML under: {0}",
        ["debugGuidIndexedFile"] = "[DEBUG][GUID] Indexed GUID {0} -> {1}",
        ["debugGuidDuplicateSkipped"] = "[DEBUG][GUID] Duplicate GUID {0} (keeping first path, skipped {1})",
        ["debugGuidSkippedFile"] = "[DEBUG][GUID] Skipped {0} — {1}",
        ["debugGuidFinishedIndexing"] = "[DEBUG][GUID] Finished indexing. Indexed: {0}, Skipped (no ' - ' pattern): {1}, Total files: {2}",

        ["debugExtractWroteFile"] = "[DEBUG][EXTRACT] Wrote {0} - {1} ({2}) -> {3} [{4}]",
        ["debugTransSourceFile"] = "[DEBUG][TRANS] Loading translation file: {0}",
        ["debugTransKeyMode"] = "[DEBUG][TRANS] Text key mode: {0} (LineId for Anno 117, GUID for Anno 1800)",
        ["debugAssetRegistrySource"] = "[DEBUG][ASSETS] Loading asset registry from: {0}",
        ["debugAssetRegistryPendingInheritance"] = "[DEBUG][ASSETS] Assets waiting on parent name inheritance: {0}",
        ["debugAssetInheritedName"] = "[DEBUG][ASSETS] Inherited name: child {0} <- parent {1} => '{2}'",
        ["debugAssetInheritPending"] = "[DEBUG][ASSETS] Pending (parent name not ready): child {0} <- parent {1}",
        ["debugAssetInheritSkipped"] = "[DEBUG][ASSETS] Skipped inheritance: child {0} — {1}",
        ["debugPropertyScanWhitelist"] = "[DEBUG][ANALYZE] Whitelist properties merged: {0}",
        ["debugPropertyEligible"] = "[DEBUG][ANALYZE] Comment-eligible property: {0}",
        ["debugOutputLayoutAnnoAssets"] = "[DEBUG][PATHS] AnnoAssets root: {0}",
        ["debugOutputLayoutGameRoot"] = "[DEBUG][PATHS] Game output root: {0}",
        ["debugOutputLayoutAssetOut"] = "[DEBUG][PATHS] Asset XML output: {0}",
        ["debugOutputLayoutModOut"] = "[DEBUG][PATHS] Asset mod output: {0}",
        ["debugOutputLayoutSingleMod"] = "[DEBUG][PATHS] Single-GUID mod output: {0}",
        ["debugPhase2SourceXml"] = "[DEBUG][PHASE] Source XML folder: {0}",
        ["debugTemplateListPath"] = "[DEBUG][TEMPLATES] Template list config: {0}",
        ["debugTemplateFixlistCount"] = "[DEBUG][TEMPLATES] Fixlist templates (full inheritance): {0}",
        ["debugMergeProcessingFile"] = "[DEBUG][MERGE] Processing file: {0} (template: {1})",
        ["debugFormatProcessingFile"] = "[DEBUG][FORMAT] Processing file: {0}",
        ["debugFormatScanRoot"] = "[DEBUG][FORMAT] Scanning output folder: {0}",
        ["debugFormatNoTemplateNode"] = "[DEBUG][FORMAT] No Template node in {0} (continuing)",
        ["debugAudioRegistrySource"] = "[DEBUG][ASSETS] Loading audio names from: {0}",

        ["debugDepStartingResolution"] = "[DEBUG][DEP] Starting dependency resolution for {0} files (excluding PaMSy).",
        ["debugDepNoBaseAssetGuid"] = "[DEBUG][DEP] No BaseAssetGUID found in {0} — skipping resolution.",
        ["debugDepResolving"] = "[DEBUG][DEP] Resolving {0} (GUID: {1})",
        ["debugDepSuccessfullyMerged"] = "[DEBUG][DEP] Successfully merged parent into {0}",
        ["debugDepCouldNotLoadParent"] = "[DEBUG][DEP] Could not load parent document for GUID {0} (file: {1})",
        ["debugDepComplete"] = "[DEBUG][DEP] Dependency resolution complete. Processed {0} files.",

        ["debugMergeBuiltXpaths"] = "[DEBUG][MERGE] Built {0} leaf xpaths for merging into {1}",
        ["debugMergeIdentifiedParentPaths"] = "[DEBUG][MERGE] Identified {0} unique parent paths to cache.",
        ["debugMergeFailedSelectPath"] = "[DEBUG][MERGE] Failed to select parent path '{0}': {1}",
        ["debugMergeCompleted"] = "[DEBUG][MERGE] Completed merging {0} nodes for {1}",

        ["modDescriptionTemplate"] = "Generated asset mod for template '{0}'.",
        ["modCategoryName"] = "Asset Splitter",
        ["readmeIntro"] = "This folder is one generated Anno Mod Loader mod for a single asset.",
        ["readmeGuideTitle"] = "# Anno Modding Guide (Asset Splitter export)",
        ["readmeGuideIntro"] = "This guide is written once per export. Start here for install paths and ModOps basics. Each mod folder also has a short README with steps for that specific asset.",
        ["readmeGuideLanguageNote"] = "Asset names and GUID comments in XML use your extraction language ({0}). README files follow the same language when a translation is available.",
        ["readmeGuideQuickStartHeader"] = "## Quick start",
        ["readmeGuideQuickStep1"] = "1. Open one mod folder that contains `modinfo.json` (not only the template browsing folder above it).",
        ["readmeGuideQuickStep2"] = "2. Copy that whole folder into your {0} mods directory (paths below).",
        ["readmeGuideQuickStep3"] = "3. Edit `modinfo.json`: set a unique `ModID`, version, and display name before sharing.",
        ["readmeGuideQuickStep4"] = "4. Edit the generated `assets.xml` patch. Prefer small `merge` changes over large `replace` blocks.",
        ["readmeGuideQuickStep5"] = "5. Enable only this mod, launch the game, and read the mod-loader log if nothing changes.",
        ["readmeGuideFolderLayoutHeader"] = "## Folder layout",
        ["readmeGuideFolderLayoutBody"] = "Template folders (for example `Building/`) group similar assets for browsing. Each standalone mod is a child folder with its own `modinfo.json`. Use `INDEX.md` inside a template folder to find assets. Do not copy the entire export tree into the game mods folder.",
        ["readmeGuidePublishHeader"] = "## Before you publish",
        ["readmeGuidePublish1"] = "- Test one generated mod at a time.",
        ["readmeGuidePublish2"] = "- Replace the generated `asset-splitter-...` ModID with your own unique ID.",
        ["readmeGuidePublish3"] = "- Reserve a GUID range for brand-new assets: https://github.com/anno-mods/GuidRanges",
        ["readmeGuidePublish4"] = "- Asset names containing Fail, Error, or Popup are normal UI assets, not signs of a broken export.",
        ["readmeShortIntro"] = "This is a ready-to-test Mod Loader mod for one game asset. Copy the folder, edit the XML patch, enable the mod, and launch the game.",
        ["readmeShortInstallHeader"] = "## Where to install",
        ["readmeShortInstall117"] = "Copy this entire folder into `<user>/Anno 117/mods/` (recommended) or `<game install>/Anno 117/mods/`.",
        ["readmeShortInstall1800"] = "Copy this entire folder into `<user>/Anno 1800/mods/` (recommended) or `<game install>/Anno 1800/mods/`.",
        ["readmeShortBrowsingNote"] = "This mod is inside the browsing folder `{0}`. Copy only `{1}` (the folder that contains `modinfo.json`), not the whole export tree.",
        ["readmeShortQuickStartHeader"] = "## Quick start",
        ["readmeShortStep1"] = "1. Copy folder `{0}` into the mods directory above. Keep `modinfo.json`, `README.md`, and `data/` together.",
        ["readmeShortStep2"] = "2. Change `ModID` and mod name in `modinfo.json` before sharing.",
        ["readmeShortStep3"] = "3. Edit `{0}` — the generated ModOps patch for this asset.",
        ["readmeShortStep4"] = "4. Enable only this mod while testing.",
        ["readmeShortStep5_117"] = "5. Launch the game. If nothing changes, open `<user>/Anno 117/mods/mod-loader.log`.",
        ["readmeShortStep5_1800"] = "5. Launch the game. If nothing changes, open `<user documents>/Anno 1800/log/mod-loader.log`.",
        ["readmeShortTryHeader"] = "## Try this first",
        ["readmeShortTryIntro"] = "Open the patch file above. The generated mod already contains a ModOp for this asset. For a safe first test, merge a small change such as the display name:",
        ["readmeShortTryMergeExample"] = "```xml\n<ModOp Type=\"merge\" GUID=\"{0}\" Path=\"/Values/Standard\">\n  <Name>{1}</Name>\n</ModOp>\n```",
        ["readmeShortTryMergeNote"] = "Add that inside the existing `<ModOps>` block (or adjust the generated ModOp). `merge` updates only the nodes you list and is safer than replacing the whole asset.",
        ["readmeShortPublishHeader"] = "## Before you share",
        ["readmeShortHelpHeader"] = "## More help",
        ["readmeShortModinfoNote"] = "The generated `ModID` is for local testing. Choose a new unique `ModID` before publishing.",
        ["readmeShortFailNameNote"] = "Names containing Fail or Error are in-game UI assets, not extraction errors.",
        ["readmeShortLearnMore"] = "Full modding guide: [{1}]({0})",
        ["readmeSummaryTitle"] = "# Asset mod export",
        ["readmeSummaryGame"] = "Game: {0}",
        ["readmeSummaryCreated"] = "Created mods: {0}",
        ["readmeSummarySkipped"] = "Skipped files: {0}",
        ["readmeSummaryIntro"] = "Each child folder with `modinfo.json` is a standalone Mod Loader mod. Template folders are for browsing only.",
        ["readmeSummaryGuide"] = "Read **[MODDING-GUIDE.md](MODDING-GUIDE.md)** for install paths, ModOps, testing, and publishing.",
        ["readmeSummaryIndex"] = "Open `INDEX.md` inside a template folder to browse assets by GUID and display name.",
        ["readmeSummaryWarning"] = "Do not copy this entire tree into the game mods folder. Copy individual mod folders only.",
        ["readmeSummarySingle"] = "Single-GUID export: one mod in this folder plus the shared modding guide.",
        ["readmeWhatIsHeader"] = "## Glossary",
        ["readmeWhatIsAsset"] = "- **Asset**: Everything in Anno is an asset — a building, an item, a specialist, a fertility, a product. Each has a unique numeric GUID and is defined in XML.",
        ["readmeWhatIsGuid"] = "- **GUID**: A unique number that identifies one specific asset. The Mod Loader uses it to know which asset to patch. Find GUIDs at a1800.net.",
        ["readmeWhatIsTemplate"] = "- **Template**: The type of asset (e.g. `Item`, `Fertility`, `FactoryBuilding7`). Templates define the default properties an asset inherits. Think of it as the asset's category.",
        ["readmeWhatIsRda"] = "- **.rda archives**: The game stores all its data in compressed `.rda` files inside the `maindata` folder. You cannot edit those directly. Instead, you create mods that get merged on top at startup.",
        ["readmeWhatIsModLoader"] = "- **Mod Loader**: A community tool that loads mods when the game starts. It reads your mod folder, finds `modinfo.json`, and merges your `assets.xml` changes into the game's data.",
        ["readmeWhatIsModOp"] = "- **ModOp (Mod Operation)**: An XML instruction that tells the Mod Loader what to change. It has a Type (what to do), GUID (which asset), and Path (where in that asset's XML to work).",
        ["readmeWhatIsModinfo"] = "- **modinfo.json**: A file at the root of every mod that tells the Mod Loader what this mod is called, who made it, and how it relates to other mods.",
        ["readmeWhatIsXPath"] = "- **XPath/Path**: A way to navigate inside XML. Like a folder path (`/Values/Standard/Name`) that points to a specific place in the asset's data tree.",
        ["readmeWhatIsModOpsWrapper"] = "- **`<ModOps>` vs `<ModOp>`**: `<ModOps>` is the outer wrapper (one per file). Inside it, you put one or more `<ModOp>` entries — each one performs a single change.",
        ["readmeWhatIsFlow"] = "- **How it works**: Game starts → Mod Loader scans your mod folder → reads `modinfo.json` → loads `assets.xml` → merges your `<ModOps>` patches on top of the game's original data → game runs with your changes.",

        ["readmeXPathHeader"] = "## XPath Tree Guide",
        ["readmeXPathIntro"] = "Every asset is a tree of XML nodes. Paths navigate this tree to find the exact spot you want to change:",
        ["readmeXPathTree"] = "Asset root → `/` (top of asset, rarely used)\n  └── Template → `/Template` (the asset type, e.g. Item, Fertility)\n  └── Values → `/Values` (where all properties live)\n      └── Standard → `/Values/Standard` (GUID, name, icon etc.)\n      │   ├── GUID → `/Values/Standard/GUID`\n      │   └── Name → `/Values/Standard/Name`\n      └── Product → `/Values/Product` (production properties)\n      │   └── Amount → `/Values/Product/Amount`\n      └── Item → `/Values/Item` (item-specific properties)\n          ├── Rarity → `/Values/Item/Rarity`\n          └── MaxStackSize → `/Values/Item/MaxStackSize`\n\nShortcut: `GUID=\"123\"` is the same as `//Asset[Values/Standard/GUID='123']`\nAnno 117 short paths skip `/Values/` — `Merge=\"Standard/Name\"` equals `Path=\"/Values/Standard/Name\"`",
        ["readmeXPathExamples"] = "- To change a name: `Path=\"/Values/Standard/Name\"`\n- To change productivity: `Path=\"/Values/FactoryUpgrade/ProductivityUpgrade/Value\"`\n- To add a new child under Item: `Path=\"/Values/Item\"` (with Type=add)\n- To insert after Standard: `Path=\"/Values/Standard\"` (with Type=addNextSibling)\n- To remove a cost node: `Path=\"/Values/Cost\"` (with Type=remove)\n- To target every asset on an island: `Path=\"//Asset[Values/Building/Island='123']\"`",

        ["readmeAssetHeader"] = "## Asset",
        ["readmeGuidLabel"] = "- GUID: `{0}`",
        ["readmeDisplayNameLabel"] = "- Display name: `{0}`",
        ["readmeInternalNameLabel"] = "- Internal name: `{0}`",
        ["readmeTemplateLabel"] = "- Template: `{0}`",
        ["readmePathHintLabel"] = "- Path hint: `{0}`",
        ["readmeGameLabel"] = "- Game: `{0}`",
        ["readmeAnnoVersionLabel"] = "- Mod Loader Anno version: `{0}`",
        ["readmeFolderHeader"] = "## Folder To Copy",
        ["readmeCopyFolder"] = "Copy this folder: `{0}`",
        ["readmeCopyHintSingle"] = "This single-GUID export puts the standalone mod directly in `{0}`. `{1}` is the folder to copy because that is where `modinfo.json` lives.",
        ["readmeCopyHintFull"] = "It may be inside a browsing folder named `{0}`. The browsing folder groups similar assets; the actual standalone mod folder is `{1}` because that is where `modinfo.json` lives.",
        ["readmeCopyWarning"] = "Do not copy only `modinfo.json`, `README.md`, or the `data` folder. Keep the whole mod folder together.",
        ["readmeFileToEditHeader"] = "## File To Edit",
        ["readmeEditFile"] = "Edit: `{0}`",
        ["readmeEditDescription"] = "That file contains the generated `<ModOps>` patch for this asset. Small edits are easiest to test by changing values inside that XML and then launching the game with only the specific mods you want enabled.",
        ["readmeModOpsHeader"] = "## ModOps (Mod Operations)",
        ["readmeModOpsIntro"] = "`<ModOps>` is the wrapper element enclosing one or more `<ModOp>` (Mod Operation) entries. Each `<ModOp>` targets a specific asset by its GUID and performs an action at the given Path within that asset's XML tree.",
        ["readmeModOpsGuid"] = "- **GUID**: Every asset in Anno has a unique numeric GUID. Use it to tell the mod loader which asset to patch. Find GUIDs at a1800.net.",
        ["readmeModOpsPath"] = "- **Path**: The nested XPath into the asset's XML where the change happens. For example, `/Values/FactoryUpgrade/ProductivityUpgrade/Value` navigates down to the productivity value node.",
        ["readmeModOpsTypes"] = "- **Type**: The kind of action. Common types are `replace`, `merge`, `add`, `addNextSibling`, `addPrevSibling`, and `remove`.",
        ["readmeModOpsWhy"] = "- The game loads assets from `.rda` archives, not loose files. The mod loader **merges** your generated `assets.xml` with the game's original at startup. Changes in your mod folder override the archived definitions without touching game files.",
        ["readmeModdingBasicsHeader"] = "## How ModOps Work",
        ["readmeBasics1"] = "- `modinfo.json` identifies the mod. Anno 117 requires `Anno: 8`; Anno 1800 recommends `Anno: 7` for better tool support.",
        ["readmeBasics2"] = "- `assets.xml` contains XML patches. The generated file uses a `ModOp` that targets this asset by GUID.",
        ["readmeBasics3"] = "- ModOps can replace, merge, add, append, prepend, or remove XML nodes.",
        ["readmeBasics4"] = "- `Merge` is usually safer than replacing large XML sections because it keeps unrelated game or modded values intact.",
        ["readmeBasics5"] = "- XPath paths point to the XML node you want to change.",
        ["readmeBasics6"] = "- Asset Splitter examples use legacy paths with `/Values/...` because that works for both Anno 117 and Anno 1800.",
        ["readmeBasics7"] = "- Anno 117 also supports a newer short syntax where `GUID=\"...\" Merge=\"Standard\"` skips `/Values/`; Anno 1800 does not.",
        ["readmeBasics8"] = "- For list-style XML, avoid relying on item order when possible; item order can change between game updates or other mods.",
        ["readmeGameRulesHeader"] = "## {0} Rules",
        ["readmeExamplesHeader"] = "## Basic ModOps Examples For This Asset",
        ["readmeExamplesIntro"] = "These examples use this asset's GUID. They are teaching examples, not extra files generated by Asset Splitter.",
        ["readmeModOpsRefIntro"] = "Each ModOp type with its description, a real example, and important notes.",

        ["readmeModOpsRefReplace"] = "### replace (Anno 117 & 1800)\nReplaces the selection at Path with your XML.\n\n```xml\n<ModOp Type=\"replace\" GUID=\"{0}\" Path=\"/Values/Standard\">\n  <Name>{1}</Name>\n</ModOp>\n```\n\n- Replaces everything at the Path with your new content\n- An empty `<ModOp Type=\"replace\" ... />` is the same as Remove\n- Avoid replacing large structures — use Merge instead for compatibility with other mods",

        ["readmeModOpsRefMerge"] = "### merge (Anno 117 & 1800)\nAdds or updates child nodes within an existing parent. Does NOT remove nodes.\n\n```xml\n<ModOp Type=\"merge\" GUID=\"{0}\" Path=\"/Values/Standard\">\n  <Name>{1}</Name>\n  <Description>Your description</Description>\n</ModOp>\n```\n\n- Safer than replace — keeps unrelated values intact\n- Order independent: child nodes can be in any order\n- Use ModItem (Anno 117) or Condition to merge list items\n- Top-level content element (e.g. `<Standard>`) can be skipped in most cases",

        ["readmeModOpsRefAdd"] = "### add (Anno 117 & 1800)\nAdds new content at the end inside the selection.\n\n```xml\n<ModOp Type=\"add\" GUID=\"{0}\" Path=\"/Values\">\n  <Maintenance />\n</ModOp>\n```\n\n- Add does NOT check if an element already exists\n- Use Condition to skip Add if the node is already present\n- Use Merge to add-if-missing or update-if-exists",

        ["readmeModOpsRefRemove"] = "### remove (Anno 117 & 1800)\nDeletes the selected element(s) at Path.\n\n```xml\n<ModOp Type=\"remove\" GUID=\"{0}\" Path=\"/Values/Cost\" />\n```\n\n- Removing a non-existing element results in a log warning\n- Use AllowNoMatch=\"1\" to suppress the warning\n- An empty Replace is the same as Remove",

        ["readmeModOpsRefAddNextSibling"] = "### addNextSibling / Append (Anno 117 & 1800)\nAdds new content right after the node at Path.\n\n```xml\n<ModOp Type=\"addNextSibling\" GUID=\"{0}\" Path=\"/Values/Standard\">\n  <Building />\n</ModOp>\n```\n\n- Anno 117 short syntax: `<ModOp GUID=\"{0}\" Append=\"Standard\">`\n- For inserting at a specific position, use Path with conditions like `Item[Building='1000178']`\n- Use ModItem Merge (Anno 117) to append list items safely",

        ["readmeModOpsRefAddPrevSibling"] = "### addPrevSibling / Prepend (Anno 117 & 1800)\nAdds new content right before the node at Path.\n\n```xml\n<ModOp Type=\"addPrevSibling\" GUID=\"{0}\" Path=\"/Values/Standard\">\n  <Building />\n</ModOp>\n```\n\n- Anno 117 short syntax: `<ModOp GUID=\"{0}\" Prepend=\"Standard\">`\n- Works identically to addNextSibling but inserts before instead of after\n- Use ModItem Merge (Anno 117) to prepend list items safely",

        ["readmeModOpsRefAsset"] = "### asset (Anno 117 only)\nPlaces an `<Asset>` directly inside `<ModOps>` without a `<ModOp>` wrapper.\n\n```xml\n<Asset>\n  <BaseAssetGUID>100780</BaseAssetGUID>\n  <Values>\n    <Standard>\n      <GUID>999999</GUID>\n      <Name>My New Asset</Name>\n    </Standard>\n  </Values>\n</Asset>\n```\n\n- Fastest way to add brand new assets\n- BaseAssetGUID order is handled automatically\n- Required workaround: add `<ModOp Add=\"/AssetList/Groups[last()]\"><Group><Assets /></Group></ModOp>` once before using asset ModOps",

        ["readmeModOpsRefHeader"] = "## ModOp Type Reference",
        ["readmeModOpsAdvHeader"] = "## Advanced ModOps Features",
        ["readmeModOpsAdvIntro"] = "Beyond the basic types, the Mod Loader supports these powerful features:",
        ["readmeModOpsAdvGroup"] = "- **Group**: Wrap multiple `<ModOp>` entries in a `<Group>` to apply a shared `Condition` to all of them at once.",
        ["readmeModOpsAdvInclude"] = "- **Include**: Load ModOps from another file with `<Include File=\"features.include.xml\" />`. Use `.include.xml` extension. Paths are relative to your mod folder.",
        ["readmeModOpsAdvCondition"] = "- **Condition**: Add `Condition=\"//Values[Standard/GUID='123']\"` to any ModOp, Group, or Include. The operation only runs if the condition matches. Use `!` prefix for negative checks.",
        ["readmeModOpsAdvModItem"] = "- **ModItem Merge** (Anno 117): Use `<ModItem Merge=\"Attribute\">` inside a `Merge` ModOp to update list items by matching an attribute value instead of by position. Supports `Append` and `Prepend` for positioning.",
        ["readmeModOpsAdvModValue"] = "- **ModValue Insert** (Anno 117): Copy existing values with `<ModValue Insert=\"@123/Standard/Name\" />`. Supports XPath calculations like `number() + 2` or `round(self::node() div 2)`.",
        ["readmeReplaceHeader"] = "### Replace This Whole Asset",
        ["readmeReplaceDesc"] = "Use this when you intentionally want your XML to replace the full asset definition.",
        ["readmeMergeHeader"] = "### Merge A Small Change",
        ["readmeMergeDesc"] = "Use this shape when you only want to change one small part of the asset. This is usually easier to combine with other mods.",
        ["readmeAddRemoveHeader"] = "### Add Or Remove A Node",
        ["readmeAddRemoveDesc"] = "Use `Add` when a node does not exist yet, and `Remove` when you want to delete one.",
        ["readmeAppendHeader"] = "### Append Or Prepend List Items",
        ["readmeAppendDesc"] = "Use these for list-like XML nodes where order matters.",
        ["readmeAssetExampleHeader"] = "### Add A Brand New Asset (Anno 117)",
        ["readmeAssetExampleDesc"] = "Drop an `<Asset>` directly inside `<ModOps>` without a `<ModOp>` wrapper. The fastest way to add a completely new asset. The order of `BaseAssetGUID` references is handled automatically.",
        ["readmeTestingHeader"] = "## Testing Tips",
        ["readmeTesting1"] = "- Test one generated asset mod at a time before enabling many of them.",
        ["readmeTesting2"] = "- If the mod does not show up, check that `modinfo.json` is directly inside the copied mod folder.",
        ["readmeTesting3_117"] = "- If the patch does not apply, check `<user>/Anno 117/mods/mod-loader.log` for missing files, dependency warnings, or XML patch errors.",
        ["readmeTesting3_1800"] = "- If the patch does not apply, check `<user documents>/Anno 1800/log/mod-loader.log` for missing files, dependency warnings, or XML patch errors.",
        ["readmeTesting4"] = "- To temporarily disable a mod by folder name, close the game first, then prefix the mod folder name with `-`.",
        ["readmeShapeHeader"] = "## Expected Shape",
        ["readmeNotesHeader"] = "## Notes",
        ["readmeNotes1"] = "- Generated asset mods are meant for testing and tinkering.",
        ["readmeNotes2"] = "- Enabling many generated asset mods at once can make troubleshooting difficult.",
        ["readmeNotes3"] = "- Use this generated mod as a starting point, then trim or rewrite the XML patch once you know exactly what value you want to change.",
        ["readmeReferencesHeader"] = "## References",
        ["readmeRefModLoader"] = "- Mod Loader: https://jakobharder.github.io/anno-mod-loader/",
        ["readmeRefFileStructure"] = "- File structure: https://jakobharder.github.io/anno-mod-loader/file-structure/",
        ["readmeRefModinfo"] = "- Modinfo: https://jakobharder.github.io/anno-mod-loader/modinfo/",
        ["readmeRefModOps"] = "- ModOps: https://jakobharder.github.io/anno-mod-loader/modops/",
        ["readmeRefModOpsBasics"] = "- ModOps basics: https://jakobharder.github.io/anno-mod-loader/modops/basics/",
        ["readmeRefDebugging"] = "- Debugging: https://jakobharder.github.io/anno-mod-loader/debug/",
        ["readmeRefGuidRanges"] = "- GUID ranges: https://github.com/anno-mods/GuidRanges",
        ["readmeRefGuidLookup"] = "- Asset lookup: https://www.a1800.net/",
        ["readmeIndexTitle"] = "# {0} mods",
        ["readmeIndexBrowseLine"] = "Browse similar assets here. Full modding help: [`{0}`](../{0}) in the export root.",
        ["readmeIndexColGuid"] = "GUID",
        ["readmeIndexColDisplayName"] = "Display name",
        ["readmeIndexColInternalName"] = "Internal name",
        ["readmeIndexColPathHint"] = "Path hint",
        ["readmeIndexColFolder"] = "Folder",
        ["readmeAnno117Install"] = "- Install location: copy the mod folder into `<user>/Anno 117/mods/` or `<install>/Anno 117/mods/`.",
        ["readmeAnno117Modinfo"] = "- `modinfo.json` is required. Anno 117 only loads mods when `modinfo.json` has `Anno: 8`.",
        ["readmeAnno117GameSetup"] = "- Anno 117 requires `Difficulty` and uses `GameSetup` for savegame, multiplayer, campaign, and safe-to-remove metadata.",
        ["readmeAnno117AssetsPath"] = "- Anno 117 uses `data/base/config/export/assets.xml` for asset XML patches.",
        ["readmeAnno117ShortModOps"] = "- Anno 117 supports newer short ModOps such as `GUID=\"123\" Merge=\"Standard\"`; those omit the `/Values/` part.",
        ["readmeAnno117LegacyModOps"] = "- Anno 117 can also use legacy ModOps with `Type=\"merge\"` and `Path=\"/Values/...\"`, which is what Asset Splitter writes for compatibility.",
        ["readmeAnno117Profile"] = "- To control activation, edit `<user>/Anno 117/mods/active-profile.txt`; prefix a mod entry with `#` to disable it.",
        ["readmeAnno117FolderDisable"] = "- You can also disable Anno 117 folders by prefixing the folder name with `-`, but the profile file is the recommended method.",
        ["readmeAnno117EnableNew"] = "- Prefix `EnableNewMods` with `#` in `active-profile.txt` if you want newly detected mods disabled by default.",
        ["readmeAnno117Deps"] = "- The Anno 117 dependency fields live under `Dependencies`: `Require`, `Optional`, `LoadAfter`, `Deprecate`, and `Incompatible`.",
        ["readmeAnno117Log"] = "- The Anno 117 mod-loader log is in `<user>/Anno 117/mods/mod-loader.log`.",
        ["readmeAnno117HotReload"] = "- Anno 117 ships with a Hot Reload mod in `<game>/mods/.ubi/hot-reload-ubi/`. When enabled, mod changes are picked up without restarting the game.",
        ["readmeAnno1800Install"] = "- Install location: copy the mod folder into `<user>/Anno 1800/mods/` or `<install>/Anno 1800/mods/`.",
        ["readmeAnno1800Fallback"] = "- Local Anno 1800 mods can fall back to the folder name if `ModID` or `modinfo.json` is missing, but Asset Splitter writes `modinfo.json` and `ModID` so tools and dependencies behave predictably.",
        ["readmeAnno1800AssetsPath"] = "- Anno 1800 uses `data/config/export/main/asset/assets.xml` for asset XML patches.",
        ["readmeAnno1800LegacyModOps"] = "- Anno 1800 must use legacy ModOps with `Type=\"...\"` and `Path=\"...\"`; do not use the Anno 117 short `Merge=\"...\"`, `Add=\"...\"`, `Remove=\"...\"`, `Append=\"...\"`, or `Prepend=\"...\"` style.",
        ["readmeAnno1800PathNote"] = "- In Anno 1800 legacy paths include `/Values/...`; the short Anno 117 paths intentionally skip `/Values/`.",
        ["readmeAnno1800AnnoField"] = "- `Anno: 7` is optional for local Anno 1800 mods, but Asset Splitter writes it because it helps tools identify the game.",
        ["readmeAnno1800Activation"] = "- To control activation, prefix the mod folder or zip with `-`, or add the mod id to `<mods>/activation.json` under `disabledIds`. Anno 1800 does not use Anno 117's `active-profile.txt`.",
        ["readmeAnno1800Zip"] = "- Anno 1800 can also load mods from `.zip` files, but island files can be problematic from zips.",
        ["readmeAnno1800Deps"] = "- Anno 1800 dependency fields are `ModDependencies`, `OptionalDependencies`, `LoadAfterIds`, `DeprecateIds`, and `IncompatibleIds`.",
        ["readmeAnno1800Log"] = "- The Anno 1800 mod-loader log is in `<user documents>/Anno 1800/log/mod-loader.log`.",
        ["readmeAnno1800NoHotReload"] = "- Anno 1800 does not support hot reload. You must restart the game to see mod changes.",

        // New non-debug phase and summary messages (improved console output)
        ["pipelineStarting"] = "Starting asset extraction pipeline...",
        ["phaseRdaAnno117"] = "\n=== PHASE 1: Extracting from {0} Anno 117 RDA archives ===",
        ["phaseRdaAnno1800"] = "\n=== PHASE 1: Extracting from {0} Anno 1800 RDA archives ===",
        ["rdaLongRunningNote"] = "This can take several minutes depending on your drive speed...\n",
        ["rdaExtractionCompleteNonDebug"] = "RDA extraction complete — processed {0}/{1} archives in {2}",
        ["rdaExtractionCompleteShort"] = "RDA extraction complete ({0}/{1} archives processed in {2})",
        ["phaseGuidIndex"] = "\n=== PHASE 2.5: Building GUID index ===",
        ["guidIndexComplete"] = "GUID index built in {0}",
        ["extractedAssetsCount"] = "Extracted {0} assets.",
        ["mergedTemplatesCount"] = "Merged inherited properties for {0} assets.",
        ["dependencyResolutionComplete"] = "Dependency resolution complete for {0} assets in {1}",
        ["translationsLoadedCount"] = "Loaded {0} translations.",
        ["assetNamesLoadedCount"] = "Loaded {0} asset names.",
        ["filesFormattedCount"] = "Formatted {0} files.",
        ["extractionCompleteHeader"] = "\n=== EXTRACTION COMPLETE ===",
        ["processedAssetsSummary"] = "Processed {0} assets.",
        ["assetModsCreatedNote"] = "Asset mod folders created.",
        ["totalTimeSummary"] = "Total time: {0}",

        // Non-debug console strings for full localization
        ["inheritingAssetNames"] = "Inheriting asset names",
        ["annotatingTemplateComments"] = "Annotating template comments",
        ["configLoadedIn"] = "Config loaded in {0}",
        ["fullInheritanceAppliedTo"] = "  Full inheritance applied to: {0}",
        ["templateFixlistNote"] = "Only assets using templates from the fixlist will receive full inherited properties.",
        ["whitelistLoadWarning"] = "Warning: Could not load whitelist {0}: {1}",
        ["progressExtracting"] = "Extracting: {0} - {1} ({2})",
        ["progressMerging"] = "Merging: {0}",
        ["assetModsProgressNonDebug"] = "Created {0} asset mod packages so far...",
        ["depResolutionLine"] = "  Resolved dependencies for: {0} <- {1}",

        // Structured issue tracking (developer summary + JSON report)
        ["issueParentNotFoundMessage"] = "Parent asset {0} was not found in the GUID file index.",
        ["issueParentNotFoundRootCause"] = "Dependency resolution could not locate the parent asset XML on disk because its GUID is missing from the index built after extraction.",
        ["issueParentNotFoundHint"] = "Parents must be indexed from the output root and the BaseAssetGUID staging folder before Phase 5 runs. Re-run with a current build if this persists after fixing the indexer.",
        ["issueParentLoadFailedMessage"] = "Parent asset {0} could not be loaded: {1}",
        ["issueParentLoadFailedRootCause"] = "The parent XML file exists in the index but failed to parse or read from disk.",
        ["issueParentLoadFailedHint"] = "Check that the parent file is valid XML and not locked by another process.",
        ["issueExtractFailedRootCause"] = "Asset extraction failed while writing the ModOp/XML file for this GUID.",
        ["issueExtractFailedHint"] = "Check disk space, path length, and whether the asset node in assets.xml is valid.",
        ["issueMergeFailedRootCause"] = "Template property merge failed for this output file.",
        ["issueMergeFailedHint"] = "Inspect templates.xml/properties.xml and the asset template name for this file.",
        ["issueFormatFailedRootCause"] = "Final formatting failed while normalizing or annotating this XML file.",
        ["issueFormatFailedHint"] = "Open the file path listed in the sample and fix malformed XML or permissions.",
        ["issueUnexpectedFileRootCause"] = "An unexpected I/O or XML error occurred during formatting.",
        ["issueUnexpectedFileHint"] = "See the detail text and the full issues JSON in the logs folder.",
        ["issueMoveTemplateRootCause"] = "The asset XML could not be moved into its template-named subfolder.",
        ["issueMoveTemplateHint"] = "Another process may hold the file open, or the destination path may be invalid.",
        ["issueModSkipRootCause"] = "The asset XML could not be turned into a mod package (missing ModOp/GUID or invalid structure).",
        ["issueModSkipHint"] = "Open the source XML and confirm it still has a valid ModOp with a GUID.",
        ["issueModReadRootCause"] = "The mod packager could not read this asset XML from disk.",
        ["issueModReadHint"] = "Verify the file exists and is readable XML.",
        ["issueTitle_ParentAssetNotInGuidIndex"] = "Parent asset not in GUID index",
        ["issueTitle_ParentAssetLoadFailed"] = "Parent asset failed to load",
        ["issueTitle_ExtractAssetFailed"] = "Asset extraction failed",
        ["issueTitle_MergeAssetFailed"] = "Template merge failed",
        ["issueTitle_FormatFileFailed"] = "Formatting failed",
        ["issueTitle_UnexpectedFileProcessingError"] = "Unexpected formatting error",
        ["issueTitle_MoveToTemplateFolderFailed"] = "Template folder move failed",
        ["issueTitle_ModPackageSkippedInvalidXml"] = "Asset mod skipped (invalid XML)",
        ["issueTitle_ModPackageReadXmlFailed"] = "Asset mod read failed",
        ["issueTitle_RdaExtractionFailed"] = "RDA extraction failed",
        ["issueTitle_PipelineFatalError"] = "Pipeline error",
        ["issueSummaryHeader"] = "=== DEVELOPER ISSUE SUMMARY ===",
        ["issueSummaryCounts"] = "Recorded issues: {0} warning(s), {1} error(s). Extraction may still finish, but these items need attention.",
        ["issueSummaryReportPath"] = "Full structured report: {0}",
        ["issueSummaryGroupLine"] = "[{0}] {1} occurrence(s)",
        ["issueSummaryGroupWithParents"] = "{0} occurrence(s), {1} unique parent GUID(s)",
        ["issueSummaryRootCausePrefix"] = "Root cause: ",
        ["issueSummaryHintPrefix"] = "Hint: ",
        ["issueSummaryMoreInReport"] = "  … and {0} more in the JSON report",
        ["issueSummarySampleChildParent"] = "Child \"{0}\" ← parent GUID {1}",
        ["issueSummarySampleGuid"] = "GUID {0} — {1}",
        ["issueSummarySampleBulletPrefix"] = "  • ",
        ["issueSummarySampleFileDetail"] = "{0} — {1}",
        ["issueSummaryNoneRecorded"] = "No structured issues were recorded (0 warnings, 0 errors).",
    };

    /// <summary>
    /// Sets the current UI language and reloads the message file.
    /// </summary>
    /// <param name="language">Language key (e.g. "english", "german") used to locate <c>console_{language}.json</c>.</param>
    public static void SetLanguage(string language)
    {
        _currentLanguage = NormalizeLanguage(language);

        LoadMessages();
    }

    /// <summary>
    /// Returns a localized message by key.
    /// Priority: current language → English fallback → embedded defaults → key itself.
    /// </summary>
    /// <param name="key">Message key (e.g. "extractingFromRda").</param>
    public static string Get(string key)
    {
        if (!_initialized)
        {
            LoadMessages();
        }

        if (_messages.TryGetValue(key, out string? message) && !string.IsNullOrEmpty(message))
        {
            return message;
        }

        if (_fallbackMessages.TryGetValue(key, out string? fallback) && !string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        if (DefaultMessages.TryGetValue(key, out string? defaultMessage))
        {
            return defaultMessage;
        }

        return key;
    }

    /// <summary>
    /// Returns a localized message for a specific language without changing the current global language.
    /// Priority: requested language → English fallback → embedded defaults → key itself.
    /// </summary>
    /// <param name="key">Message key.</param>
    /// <param name="language">Language code (e.g. "russian", "german").</param>
    public static string GetForLanguage(string key, string language)
    {
        string normalized = NormalizeLanguage(language);
        Dictionary<string, string> messages = LoadMessagesFromFile(normalized);
        if (messages.TryGetValue(key, out string? msg) && !string.IsNullOrEmpty(msg))
            return msg;

        if (normalized != EnglishLanguage)
        {
            messages = LoadMessagesFromFile(EnglishLanguage);
            if (messages.TryGetValue(key, out msg) && !string.IsNullOrEmpty(msg))
                return msg;
        }

        if (DefaultMessages.TryGetValue(key, out string? dm) && !string.IsNullOrEmpty(dm))
            return dm;

        return key;
    }

    /// <summary>Initialises _messages for the current language and _fallbackMessages for English (or DefaultMessages when already English).</summary>
    private static void LoadMessages()
    {
        _initialized = true;
        _messages = LoadMessagesFromFile(_currentLanguage);
        _fallbackMessages = _currentLanguage != EnglishLanguage
          ? LoadMessagesFromFile(EnglishLanguage)
          : new(DefaultMessages);
    }

    /// <summary>Parses console_&lt;language&gt;.json from disk; returns DefaultMessages for english, or an empty dict when a non-English file is missing or unreadable.</summary>
    private static Dictionary<string, string> LoadMessagesFromFile(string language)
    {
        string? filePath = GetConfigPath(language);

        if (filePath is not null && File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                Dictionary<string, string>? messages = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (messages is not null)
                {
                    return messages;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Silently fall back to defaults on any parse/IO error.
            }
        }

        return language == EnglishLanguage ? new(DefaultMessages) : [];
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return EnglishLanguage;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            "english" => EnglishLanguage,
            "german" => "de",
            "french" => "fr",
            "spanish" => "es",
            "italian" => "it",
            "polish" => "pl",
            "russian" => "ru",
            "japanese" => "ja",
            "korean" => "ko",
            "chinese" or "simplified_chinese" => "zh",
            "traditional_chinese" => "tw",
            var normalized => normalized
        };
    }

    /// <summary>Returns the first existing file path for console_&lt;language&gt;.json across production, dev, and relative path candidates; null if none exist.</summary>
    private static string? GetConfigPath(string language)
    {
        string filename = $"console_{language}.json";
        var resolved = ConfigPathResolver.Resolve(ConsoleMessagesFolder, filename);
        return string.IsNullOrEmpty(resolved) ? null : resolved;
    }
}
