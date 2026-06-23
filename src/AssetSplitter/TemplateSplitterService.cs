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
            {
                context.Log.Write("WARNING", ConsoleMessages.Get("templateSplitMissingTemplatesXml"));
            }

            return;
        }

        string outputBase = Path.Combine(gameOutputRoot, "output_templates_" + gameType);
        var templatesDoc = new XmlDocument();
        templatesDoc.Load(templatesPath);
        XmlNodeList? templateNodes = templatesDoc.DocumentElement?.SelectNodes("//Template");
        if (templateNodes == null || templateNodes.Count == 0)
        {
            if (context.DebugMode)
            {
                context.Log.Write("WARNING", ConsoleMessages.Get("templateSplitNoTemplateNodes"));
            }

            return;
        }

        int count = 0;
        List<string> savedPaths = [];
        HashSet<string> usedFileNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (XmlNode? templateNode in templateNodes)
        {
            if (templateNode == null)
            {
                continue;
            }

            string name = templateNode.Attributes?.GetNamedItem("Name")?.Value?.Trim()
              ?? templateNode.SelectSingleNode("Name")?.InnerText?.Trim()
              ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string safeName = AssetTextSanitizer.SanitizeFileNamePart(name, 90);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                continue;
            }

            string fileName = ReserveUniqueTemplateFileName(safeName, usedFileNames);
            string filePath = Path.Combine(outputBase, fileName);
            var outDoc = new XmlDocument();
            outDoc.AppendChild(outDoc.ImportNode(templateNode, true));
            GeneratedXmlFootprint.Save(outDoc, filePath, context);
            savedPaths.Add(filePath);
            count++;
            if (context.DebugMode)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugTemplateSplitFile"), safeName));
            }
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
        {
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("okWithMessage"), message));
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private static string ReserveUniqueTemplateFileName(string safeName, HashSet<string> usedFileNames)
    {
        string baseName = safeName;
        string fileName = baseName + ".xml";
        int suffix = 2;
        while (!usedFileNames.Add(fileName))
        {
            string suffixText = "-" + suffix;
            int maxBaseLength = Math.Max(1, 90 - suffixText.Length);
            string trimmedBaseName = baseName.Length <= maxBaseLength
                ? baseName
                : baseName[..maxBaseLength].TrimEnd(' ', '-', '_');
            if (string.IsNullOrWhiteSpace(trimmedBaseName))
            {
                trimmedBaseName = "Template";
            }

            fileName = trimmedBaseName + suffixText + ".xml";
            suffix++;
        }

        return fileName;
    }
}
