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
            return;

        string gameDisplayName = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase) ? "Anno 117" : "Anno 1800";
        string extractMsgKey = context.AssetModOpsWrap ? "extractingToModOp" : "extractingToXml";
        if (context.DebugMode)
        {
            string formatDesc = ConsoleMessages.Get(context.AssetModOpsWrap ? "extractFormatModOp" : "extractFormatXml");
            context.Log.Write("EXTRACT", string.Format(ConsoleMessages.Get("extractConvertingAssets"), gameDisplayName, formatDesc));
            context.Log.Write("INFO", ConsoleMessages.Get("extractCreatingXmlMods"));
        }
        else
        {
            Console.WriteLine($"\n{ConsoleMessages.Get(extractMsgKey).Replace("{0}", gameDisplayName)}");
            // Progress is handled via OutputFixer (progress bar). Avoid raw spam here.
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
                continue;

            XmlNode? assetNode = documentElement.SelectSingleNode($"//Asset[Values/Standard/GUID='{currentGuid}']");
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
            string guid = XmlNodeText.GetValue(clonedAsset, "//Values/Standard/GUID");
            string name = XmlNodeText.GetValue(clonedAsset, "//Values/Standard/Name");
            string templateName = XmlNodeText.GetValue(clonedAsset, "Template");
            string baseAssetGuid = XmlNodeText.GetValue(clonedAsset, "BaseAssetGUID");

            if (string.IsNullOrEmpty(guid))
                continue;

            if (string.IsNullOrEmpty(name))
                name = guid;

            string filename = MakeFileName(context, guid, name, templateName);
            XmlDocument xmlDocument = CreateExtractedAssetDocument(clonedAsset, guid);

            string outPath = !string.IsNullOrEmpty(baseAssetGuid)
                ? Path.Combine(context.AssetOut, "BaseAssetGUID", filename)
                : Path.Combine(context.AssetOut, filename);

            if (!string.IsNullOrEmpty(baseAssetGuid))
                Directory.CreateDirectory(Path.Combine(context.AssetOut, "BaseAssetGUID"));

            AssetDocumentSaver.SaveExtractedAsset(xmlDocument, outPath, !context.AssetModOpsWrap);

            if (!string.IsNullOrEmpty(baseAssetGuid) && context.AssetFix && !visited.Contains(baseAssetGuid))
                pending.Enqueue(baseAssetGuid);
        }

        context.ProgressReporter.OutputFixer(ConsoleMessages.Get("extractingProgress"), "1", "1");
    }

    private static void ExtractAllMatchingAssets(PipelineContext context, XmlNode documentElement, string gameType)
    {
        XmlNodeList? xmlNodeList = documentElement.SelectNodes("//Assets/Asset");
        if (xmlNodeList is null)
            return;

        context.TemplatesUsed = context.AssetTemplatesList ?? TemplateLoader.LoadTemplates(gameType);
        HashSet<string> templateSet = new(context.TemplatesUsed, StringComparer.OrdinalIgnoreCase);

        // Pre-filter to only assets that will actually be processed, so the total is accurate
        XmlNode[] allAssets = [.. xmlNodeList.Cast<XmlNode>()];
        List<XmlNode> matchingAssets = new(allAssets.Length);
        foreach (XmlNode node in allAssets)
        {
            string tpl = XmlNodeText.GetValue(node, "Template");
            string baseGuid = XmlNodeText.GetValue(node, "BaseAssetGUID");
            if (!string.IsNullOrEmpty(baseGuid) || templateSet.Contains(tpl))
                matchingAssets.Add(node);
        }

        int count = matchingAssets.Count;
        if (count == 0)
            return;

        Directory.CreateDirectory(Path.Combine(context.AssetOut, "BaseAssetGUID"));
        string baseAssetGuidDir = Path.Combine(context.AssetOut, "BaseAssetGUID");
        int processedCount = 0;
        string lastProgressMessage = ConsoleMessages.Get("extractingProgress");
        Lock lockObj = new();

        Parallel.ForEach(
            matchingAssets,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            assetNode =>
            {
                XmlNode clonedAsset = assetNode.CloneNode(true);
                string guid = XmlNodeText.GetValue(clonedAsset, "//Values/Standard/GUID");
                string name = XmlNodeText.GetValue(clonedAsset, "//Values/Standard/Name");
                string templateName = XmlNodeText.GetValue(clonedAsset, "Template");
                string baseAssetGuid = XmlNodeText.GetValue(clonedAsset, "BaseAssetGUID");

                bool hasBase = !string.IsNullOrEmpty(baseAssetGuid);

                if (string.IsNullOrEmpty(name))
                    name = guid;

                if (guid.Length == 0)
                    return;

                try
                {
                    string fileName = MakeFileName(context, guid, name, templateName);
                    XmlDocument localDoc = CreateExtractedAssetDocument(clonedAsset, guid);

                    string outputPath = hasBase
                        ? Path.Combine(baseAssetGuidDir, fileName).Replace("\n", "").Replace("\r", "")
                        : Path.Combine(context.AssetOut, fileName).Replace("\n", "").Replace("\r", "");

                    AssetDocumentSaver.SaveExtractedAsset(localDoc, outputPath, !context.AssetModOpsWrap);

                    if (context.DebugMode)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugExtractWroteFile"),
                            guid,
                            name,
                            templateName,
                            outputPath,
                            hasBase ? "BaseAssetGUID" : "main"));
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
                            lastProgressMessage = progressMsg;
                        context.ProgressReporter.OutputFixer(progressMsg, currentCount.ToString(), count.ToString());
                    }
                }
                catch (Exception ex)
                {
                    context.Issues.ReportExtractAssetFailed(guid, ex.Message);
                    context.Log.Write("ERROR", string.Format(ConsoleMessages.Get("extractAssetFailed"), guid, ex.Message));
                }
            });

        string finalProgressMessage;
        lock (lockObj)
            finalProgressMessage = lastProgressMessage;
        context.ProgressReporter.OutputFixer(finalProgressMessage, processedCount.ToString(), processedCount.ToString());

        if (context.DebugMode)
        {
            context.Log.Write("COMPLETE", $"{ConsoleMessages.Get("doneLabel")} {ConsoleMessages.Get("extractionComplete")}");
        }
        else
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("extractedAssetsCount"), processedCount.ToString("N0")));
        }
    }

    private static string FormatSingleGuidNotFoundMessage(string guid)
    {
        const string key = "singleGuidNotFound";
        string messageText = ConsoleMessages.Get(key);
        if (string.IsNullOrEmpty(messageText) || messageText == key)
            return $"[WARNING] GUID '{guid}' not found in game assets. Check the GUID or run full extraction first.";

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
}
