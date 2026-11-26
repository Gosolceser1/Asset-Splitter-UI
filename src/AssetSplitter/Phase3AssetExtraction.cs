using System.Xml;

namespace AssetProcessor;

internal static class Phase3AssetExtraction
{
    internal static int ExecutePhase3AssetExtraction(
        PipelineContext context,
        string singleAssetGuid,
        string gameType,
        string gameOutputRoot,
        out string[] mainOutputFiles,
        out string[] baseAssetGuidFiles)
    {
        mainOutputFiles = [];
        baseAssetGuidFiles = [];

        if (!OutputDirectoryManager.TryPrepareAssetOutputDirectory(context))
        {
            return 1;
        }

        XmlDocument xmlSourceFile = new();
        string assetsPath = Path.Combine(context.SourceXmlFolder, "assets.xml");
        try
        {
            XmlDocumentLoadRetry.Load(xmlSourceFile, assetsPath);
        }
        catch (XmlException)
        {
            xmlSourceFile = new XmlDocument();
            try
            {
                XmlDocumentLoadRetry.Load(xmlSourceFile, assetsPath, Encoding.Unicode);
            }
            catch (XmlException)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "assetsXmlDocumentElementLoadFailed", always: true);
                return 1;
            }
        }
        if (xmlSourceFile.DocumentElement == null)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "assetsXmlDocumentElementLoadFailed", always: true);
            return 1;
        }
        AssetExtractor.ExtractAssets(context, xmlSourceFile, singleAssetGuid, gameType);
        Console.WriteLine();

        if (!string.IsNullOrEmpty(singleAssetGuid))
        {
            if (!Directory.EnumerateFiles(context.AssetOutputDir, "*.xml", SearchOption.AllDirectories).Any())
            {
                TryCleanEmptySingleAssetOutput(context);
                return 1;
            }
            else
            {
                string normalizedGameType = GameTypeDetector.IsAnno117(gameType)
                    ? GameTypeDetector.Anno117
                    : GameTypeDetector.Anno1800;
                RenameSingleAssetOutputFolder(context, normalizedGameType);
            }
        }

        string[] allOutputFiles = AssetProcessorFileSystem.FileList(context.AssetOutputDir);
        string baseAssetGuidDir = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        string[] allBaseFiles = Directory.Exists(baseAssetGuidDir) ? AssetProcessorFileSystem.FileList(baseAssetGuidDir) : [];

        if (context.RepairOnlyGuids is not null)
        {
            // Enrich only newly extracted files; GuidIndex still sees the full tree via CollectGuidIndexFilePaths.
            // Empty RepairOnlyGuids = mod-only repair (no XML re-extract).
            mainOutputFiles = context.RepairOutputFiles
                .Where(path => File.Exists(path) && !IsUnderBaseStaging(context, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            baseAssetGuidFiles = context.RepairOutputFiles
                .Where(path => File.Exists(path) && IsUnderBaseStaging(context, path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (mainOutputFiles.Length == 0 && baseAssetGuidFiles.Length == 0)
            {
                // Mod-only gaps: keep empty enrich lists; exporter will recreate packages from existing XML.
                mainOutputFiles = [];
                baseAssetGuidFiles = [];
            }
        }
        else
        {
            mainOutputFiles = allOutputFiles;
            baseAssetGuidFiles = allBaseFiles;
        }

        if (!string.IsNullOrEmpty(singleAssetGuid) && mainOutputFiles.Length == 0 && baseAssetGuidFiles.Length == 0)
        {
            return 1;
        }

        return 0;
    }

    private static bool IsUnderBaseStaging(PipelineContext context, string path)
    {
        // Honour the configured staging folder name (app_settings.json "baseasset_folder").
        string stagingFolderName = Path.GetFileName(OutputDirectoryManager.GetBaseAssetGuidStagingPath(context));
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/" + stagingFolderName + "/", StringComparison.OrdinalIgnoreCase);
    }

    internal static void TryCleanEmptySingleAssetOutput(PipelineContext context)
    {
        try
        {
            if (Directory.Exists(context.SingleAssetOutputRoot))
            {
                Directory.Delete(context.SingleAssetOutputRoot, true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugCleanSingleGuidOutputFailed"), ex.Message));
        }
    }

    internal static void RenameSingleAssetOutputFolder(PipelineContext context, string normalizedGameType)
    {
        // After single GUID extraction, read the display name from the output XML file
        // and rename the output folder to include the asset name.
        string assetName = "Unknown Asset";
        try
        {
            string[] outputFiles = Directory.GetFiles(context.AssetOutputDir, "*.xml", SearchOption.AllDirectories);
            if (outputFiles.Length > 0)
            {
                // With -f the requested child sits in the staging folder and its ancestors at the top
                // level; enumeration lists top-level files first, so pick the requested GUID's file.
                string requestedPrefix = context.SingleAssetGuid + " - [";
                string chosenFile = outputFiles.FirstOrDefault(path =>
                        Path.GetFileName(path).StartsWith(requestedPrefix, StringComparison.OrdinalIgnoreCase))
                    ?? outputFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(chosenFile);
                int bracketStart = fileName.IndexOf(" - [", StringComparison.Ordinal);
                if (bracketStart >= 0)
                {
                    int nameStart = bracketStart + 4;
                    int nameEnd = fileName.LastIndexOf(']');
                    if (nameEnd > nameStart)
                    {
                        assetName = fileName[nameStart..nameEnd].Trim();
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugReadAssetNameFromOutputFailed"), ex.Message));
        }

        context.SingleAssetDisplayName = assetName;
        string safeGuid = AssetTextSanitizer.SanitizeFileNamePart(context.SingleAssetGuid, 40);
        string safeName = AssetTextSanitizer.SanitizeFileNamePart(assetName, 80);
        string newFolderName = $"{safeGuid} - {safeName}";

        string gameOutputRoot = context.GameOutputRoot;
        string singleGuidXmlRoot = Path.Combine(gameOutputRoot, "single_guid_output_xml_" + normalizedGameType);
        string newXmlPath = Path.Combine(singleGuidXmlRoot, newFolderName);
        string newModPath = Path.Combine(gameOutputRoot, "single_guid_mods", newFolderName);

        if (!newXmlPath.Equals(context.SingleAssetOutputRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(context.SingleAssetOutputRoot))
            {
                if (Directory.Exists(newXmlPath))
                {
                    Directory.Delete(newXmlPath, true);
                }

                if (!OutputDirectoryManager.EnsureSingleGuidXmlContainer(context, normalizedGameType))
                {
                    return;
                }

                Directory.Move(context.SingleAssetOutputRoot, newXmlPath);
            }
            context.SingleAssetOutputRoot = newXmlPath;
            context.AssetOutputDir = newXmlPath;
        }

        context.SingleAssetModOutputRoot = newModPath;
        if (!string.IsNullOrWhiteSpace(context.SingleAssetDisplayName))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "singleGuidAsset", always: true, context.SingleAssetGuid, context.SingleAssetDisplayName);
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "singleGuidXmlOutput", always: true, newXmlPath);
        }
    }
}
