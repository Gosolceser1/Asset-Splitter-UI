using System.Xml;

namespace AssetProcessor;

public static class TemplateSplitterService
{
    public static void SplitTemplatesIntoFolders(PipelineContext context, string sourceXmlFolder, string gameOutputRoot, string gameType)
    {
        string templatesPath = Path.Combine(sourceXmlFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), "templates.xml");
        if (!File.Exists(templatesPath))
        {
            if (context.DebugMode)
                context.Log.Write("WARNING", ConsoleMessages.Get("templateSplitMissingTemplatesXml"));
            return;
        }

        string outputBase = Path.Combine(gameOutputRoot, "output_templates_" + gameType);
        Directory.CreateDirectory(outputBase);
        var templatesDoc = new XmlDocument();
        templatesDoc.Load(templatesPath);
        XmlNodeList? templateNodes = templatesDoc.DocumentElement?.SelectNodes("//Template");
        if (templateNodes == null || templateNodes.Count == 0)
        {
            if (context.DebugMode)
                context.Log.Write("WARNING", ConsoleMessages.Get("templateSplitNoTemplateNodes"));
            return;
        }

        int count = 0;
        List<string> savedPaths = [];
        foreach (XmlNode? templateNode in templateNodes)
        {
            if (templateNode == null) continue;
            string name = templateNode.Attributes?.GetNamedItem("Name")?.Value?.Trim()
              ?? templateNode.SelectSingleNode("Name")?.InnerText?.Trim()
              ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            string safeName = AssetTextSanitizer.SanitizeFileNamePart(name);
            if (string.IsNullOrWhiteSpace(safeName)) continue;
            string filePath = Path.Combine(outputBase, safeName + ".xml");
            var outDoc = new XmlDocument();
            outDoc.AppendChild(outDoc.ImportNode(templateNode, true));
            outDoc.Save(filePath);
            savedPaths.Add(filePath);
            count++;
            if (context.DebugMode)
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugTemplateSplitFile"), safeName));
        }

        if (context.DebugMode)
        {
            int loggedFileNames = savedPaths.Count;
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugTemplateSplitSummary"),
                count.ToString("N0"),
                templatesPath,
                outputBase,
                loggedFileNames.ToString("N0")));
        }

        string message = string.Format(ConsoleMessages.Get("splitTemplatesComplete"), count, outputBase);
        if (context.DebugMode)
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("okWithMessage"), message));
        else
            Console.WriteLine(message);

        if (context.AssetComments && savedPaths.Count > 0)
        {
            if (!context.DebugMode)
                Console.WriteLine(ConsoleMessages.Get("annotatingTemplateComments"));
            FormattingService.AnnotateFilesWithGuidComments(context, [.. savedPaths], context.PropertyScan!);
        }
    }
}
