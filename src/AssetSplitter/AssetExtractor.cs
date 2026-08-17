using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace AssetProcessor;

public static class AssetExtractor
{
    public static void ExtractAssets(PipelineContext context, XmlDocument xmlSourceFile, string singleGuidFilter = "", string gameType = "anno1800")
    {
        XmlNode? documentElement = xmlSourceFile.DocumentElement;
        if (documentElement is null)
        {
            return;
        }

        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";
        string extractMsgKey = context.ModOpsWrap ? "extractingToModOp" : "extractingToXml";
        if (context.DebugMode)
        {
            string formatDesc = ConsoleMessages.Get(context.ModOpsWrap ? "extractFormatModOp" : "extractFormatXml");
            context.Log.WriteLocalized(ConsoleMessageKind.Extract, "extractConvertingAssets", always: true, gameDisplayName, formatDesc);
            context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("extractCreatingXmlMods"));
        }
        else
        {
            Console.WriteLine();
            BackendI18nLine.Write(extractMsgKey, gameDisplayName);
            // Progress is handled via ReportProgress (progress bar). Avoid raw spam here.
        }

        if (singleGuidFilter.Length > 0)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugSingleGuidFilter"), singleGuidFilter, singleGuidFilter.Length));
            ExtractSingleAsset(context, xmlSourceFile, documentElement, singleGuidFilter, gameType);
            return;
        }

        ExtractAllMatchingAssets(context, documentElement, gameType);
    }

    private static void ExtractSingleAsset(
        PipelineContext context,
        XmlDocument xmlSourceFile,
        XmlNode documentElement,
        string singleGuidFilter,
        string gameType)
    {
        context.Log.Debug(ConsoleMessages.Get("debugSingleGuidModeActive"));

        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        Queue<string> pending = new();
        pending.Enqueue(singleGuidFilter);

        while (pending.TryDequeue(out string? currentGuid) && currentGuid is not null)
        {
            if (!visited.Add(currentGuid))
            {
                continue;
            }

            XmlNode? assetNode = FindAssetByGuid(documentElement, currentGuid);
            if (assetNode is null)
            {
                if (currentGuid == singleGuidFilter)
                {
                    Console.WriteLine(FormatSingleGuidNotFoundMessage(currentGuid));
                    return;
                }
                continue;
            }

            XmlNode clonedAsset = assetNode.CloneNode(true);
            string guid = XmlNodeText.GetValue(clonedAsset, "Values/Standard/GUID");
            string name = XmlNodeText.GetValue(clonedAsset, "Values/Standard/Name");
            string templateName = XmlNodeText.GetValue(clonedAsset, "Template");
            string baseAssetGuid = XmlNodeText.GetValue(clonedAsset, "BaseAssetGUID");

            if (string.IsNullOrEmpty(guid))
            {
                continue;
            }

            if (string.IsNullOrEmpty(name))
            {
                name = guid;
            }

            string filename = MakeFileName(context, guid, name, templateName);
            XmlDocument xmlDocument = CreateExtractedAssetDocument(clonedAsset, guid);

            string outPath = GetExtractOutputPath(context, filename, !string.IsNullOrEmpty(baseAssetGuid));

            AssetDocumentSaver.SaveExtractedAsset(context, xmlDocument, outPath, !context.ModOpsWrap);

            if (!string.IsNullOrEmpty(baseAssetGuid) && context.FixDependencies && !visited.Contains(baseAssetGuid))
            {
                pending.Enqueue(baseAssetGuid);
            }
        }

        context.ProgressReporter.ReportProgress(ConsoleMessages.Get("extractingProgress"), "1", "1");
    }

    private static void ExtractAllMatchingAssets(PipelineContext context, XmlNode documentElement, string gameType)
    {
        XmlNodeList? xmlNodeList = documentElement.SelectNodes("//Assets/Asset");
        List<XmlNode> candidateAssets = [];
        if (xmlNodeList is not null)
        {
            foreach (XmlNode node in xmlNodeList)
            {
                candidateAssets.Add(node);
            }
        }

        if (documentElement.Name == "Asset")
        {
            candidateAssets.Add(documentElement);
        }
        else
        {
            XmlNodeList? directAssets = documentElement.SelectNodes("./Asset");
            if (directAssets is not null)
            {
                foreach (XmlNode node in directAssets)
                {
                    if (!candidateAssets.Contains(node))
                    {
                        candidateAssets.Add(node);
                    }
                }
            }
        }

        if (candidateAssets.Count == 0)
        {
            return;
        }

        context.TemplatesUsed = context.AssetTemplatesList ?? TemplateLoader.LoadTemplates(gameType);
        HashSet<string> templateSet = new(context.TemplatesUsed, StringComparer.OrdinalIgnoreCase);

        // Pre-filter to only assets that will actually be processed, so the total is accurate
        XmlNode[] allAssets = [.. candidateAssets];
        List<XmlNode> matchingAssets = new(allAssets.Length);
        HashSet<string>? repairOnly = context.RepairOnlyGuids;
        foreach (XmlNode node in allAssets)
        {
            string tpl = XmlNodeText.GetValue(node, "Template");
            string baseGuid = XmlNodeText.GetValue(node, "BaseAssetGUID");
            if (!string.IsNullOrEmpty(baseGuid) || templateSet.Contains(tpl))
            {
                if (repairOnly is not null)
                {
                    string guid = XmlNodeText.GetValue(node, "Values/Standard/GUID");
                    if (guid.Length == 0 || !repairOnly.Contains(guid))
                    {
                        continue;
                    }
                }

                matchingAssets.Add(node);
            }
        }

        int count = matchingAssets.Count;
        if (count == 0)
        {
            return;
        }

        // Announce extract work before the loop so the UI overall bar re-anchors
        // (Phase 1 / language finishing must not leave OverallPercent stuck at 100%).
        Console.WriteLine($"[PLAN+] {count}");

        int processedCount = 0;
        string lastProgressMessage = ConsoleMessages.Get("extractingProgress");
        Lock lockObj = new();
        var usedOutputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Parallel.ForEach(
            matchingAssets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = PipelineParallelism.ForXmlFileIo(context),
                CancellationToken = context.CancellationToken,
            },
            assetNode =>
            {
                XmlNode clonedAsset = assetNode.CloneNode(true);
                string guid = XmlNodeText.GetValue(clonedAsset, "Values/Standard/GUID");
                string name = XmlNodeText.GetValue(clonedAsset, "Values/Standard/Name");
                string templateName = XmlNodeText.GetValue(clonedAsset, "Template");
                string baseAssetGuid = XmlNodeText.GetValue(clonedAsset, "BaseAssetGUID");

                bool hasBase = !string.IsNullOrEmpty(baseAssetGuid);

                if (string.IsNullOrEmpty(name))
                {
                    name = guid;
                }

                if (guid.Length == 0)
                {
                    return;
                }

                try
                {
                    string fileName = MakeFileName(context, guid, name, templateName);
                    XmlDocument localDoc = CreateExtractedAssetDocument(clonedAsset, guid);

                    string outputPath = GetExtractOutputPath(context, fileName, hasBase).Replace("\n", "").Replace("\r", "");
                    lock (lockObj)
                    {
                        outputPath = ReserveUniqueOutputPath(outputPath, usedOutputPaths);
                    }

                    AssetDocumentSaver.SaveExtractedAsset(context, localDoc, outputPath, !context.ModOpsWrap);

                    if (repairOnly is not null)
                    {
                        lock (lockObj)
                        {
                            context.RepairOutputFiles.Add(outputPath);
                        }

                        OutputRunCache.TryWriteRepairActionLine(
                            context,
                            guid,
                            OutputRunCache.FormatGuidAndName(guid, name),
                            OutputRunCache.TryMapItemKeyToExtractRepairActionKey,
                            ConsoleMessageKind.Ok);
                    }

                    if (context.DebugMode)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugExtractWroteFile"),
                            guid,
                            name,
                            templateName,
                            outputPath,
                            hasBase && context.FixDependencies ? "BaseAssetGUID" : "main"));
                    }

                    int currentCount;
                    lock (lockObj)
                    {
                        processedCount++;
                        currentCount = processedCount;
                    }

                    int progressBarInterval = context.DebugMode ? 1 : 100;
                    if (DeveloperTrace.ShouldReportProgress(context, currentCount, count, progressBarInterval))
                    {
                        string progressMsg = string.Format(ConsoleMessages.Get("progressExtracting"), guid, name, templateName);
                        lock (lockObj)
                        {
                            lastProgressMessage = progressMsg;
                        }

                        context.ProgressReporter.ReportProgress(progressMsg, currentCount.ToString(), count.ToString());
                    }
                }
                catch (Exception ex)
                {
                    context.Issues.ReportExtractAssetFailed(guid, ex.Message);
                    context.Log.WriteLocalized(ConsoleMessageKind.Error, "extractAssetFailed", always: true, guid, ex.Message);
                }
            });

        string finalProgressMessage;
        lock (lockObj)
        {
            finalProgressMessage = lastProgressMessage;
        }

        context.ProgressReporter.ReportProgress(finalProgressMessage, processedCount.ToString(), processedCount.ToString());

        if (context.DebugMode)
        {
            context.Log.Write(ConsoleMessageKind.Complete, $"{ConsoleMessages.Get("doneLabel")} {ConsoleMessages.Get("extractionComplete")}");
        }
        else
        {
            BackendI18nLine.Write("extractedAssetsCount", processedCount.ToString("N0"));
        }
    }

    private static XmlNode? FindAssetByGuid(XmlNode documentElement, string guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return null;
        }

        XmlNodeList? assets = documentElement.SelectNodes("//Asset");
        if (assets is null)
        {
            return null;
        }

        foreach (XmlNode asset in assets)
        {
            if (XmlNodeText.GetValue(asset, "Values/Standard/GUID") == guid)
            {
                return asset;
            }
        }

        return null;
    }

    private static string FormatSingleGuidNotFoundMessage(string guid)
    {
        const string key = "singleGuidNotFound";
        string messageText = ConsoleMessages.Get(key);
        if (string.IsNullOrEmpty(messageText) || messageText == key)
        {
            return $"[WARNING] GUID '{guid}' not found in game assets. Check the GUID or run full extraction first.";
        }

        return string.Format(messageText, guid);
    }

    private static XmlDocument CreateExtractedAssetDocument(XmlNode assetNode, string guid)
    {
        XmlDocument document = new();
        XmlElement modOpsElement = document.CreateElement("ModOps");
        document.AppendChild(modOpsElement);

        XmlElement modOpElement = document.CreateElement("ModOp");
        modOpElement.SetAttribute("GUID", guid);
        modOpElement.SetAttribute("Type", "Replace");
        modOpElement.SetAttribute("Path", "/");
        modOpsElement.AppendChild(modOpElement);

        XmlNode importedAsset = document.ImportNode(assetNode, true);
        modOpElement.AppendChild(importedAsset);
        return document;
    }

    private static string MakeFileName(PipelineContext context, string guid, string name, string template = "")
    {
        string translatedName = context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
          ? ""
          : TranslationRegistry.Translate(context, guid);

        return AssetTextSanitizer.FormatAssetFileName(guid, name, template, translatedName);
    }

    /// <summary>Staging folder is only used when <see cref="PipelineContext.FixDependencies"/> (-f) will merge parents in Phase 6.</summary>
    private static string GetExtractOutputPath(PipelineContext context, string fileName, bool hasBaseAssetGuid)
    {
        if (hasBaseAssetGuid && context.FixDependencies)
        {
            return Path.Combine(OutputDirectoryManager.EnsureBaseAssetGuidStagingDirectory(context), fileName);
        }

        return Path.Combine(context.AssetOutputDir, fileName);
    }

    private static string ReserveUniqueOutputPath(string outputPath, HashSet<string> usedOutputPaths)
    {
        string directory = Path.GetDirectoryName(outputPath) ?? "";
        string fileName = Path.GetFileName(outputPath);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = outputPath;
        int suffix = 2;
        while (true)
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (!File.Exists(fullCandidate) && usedOutputPaths.Add(fullCandidate))
            {
                return candidate;
            }

            candidate = Path.Combine(directory, $"{stem}-{suffix}{extension}");
            suffix++;
        }
    }
}
