using System.Xml;

namespace AssetProcessor;

public static class PropertyScanner
{
    public static PropertyScanResult Scan(PipelineContext context)
    {
        string filename = AssetProcessorFileSystem.CombineSourceXml(context, "properties.xml");
        XmlDocument xmlDocument = new();
        try
        {
            XmlDocumentLoadRetry.Load(xmlDocument, filename);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "xmlFileNotFound", always: true, filename);
            context.Issues.ReportUnexpectedFileError($"{filename}: {ex.Message}");
            return new PropertyScanResult([], []);
        }
        XmlNodeList? xmlNodeList = xmlDocument.DocumentElement?.SelectNodes("//*");
        HashSet<string> eligible = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> whitelist = new(StringComparer.OrdinalIgnoreCase);

        var propertyExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "None", "Deuteranopia", "Protanopia", "Tritanopia", "ColorMode"
        };

        context.Log.Write(ConsoleMessageKind.Analyze, ConsoleMessages.Get("propertyScanAnalyze"));
        context.Log.Write(ConsoleMessageKind.Info, ConsoleMessages.Get("propertyScanInfo"));

        if (xmlNodeList is not null)
        {
            foreach (XmlNode xmlNode in xmlNodeList)
            {
                if (!propertyExclusions.Contains(xmlNode.Name) && xmlNode.InnerText == "0")
                {
                    eligible.Add(xmlNode.Name);
                }
            }
        }

        string[] whitelistProperties = CommentWhitelistLoader.Load(
            GameTypeDetector.DetectFromPath(context.AssetRoot),
            AppDomain.CurrentDomain.BaseDirectory,
            out string? whitelistWarning);

        if (whitelistWarning is not null)
        {
            Console.WriteLine(whitelistWarning);
        }

        foreach (string propertyName in whitelistProperties)
        {
            eligible.Add(propertyName);
            whitelist.Add(propertyName);
        }

        context.Log.WriteLocalized(ConsoleMessageKind.Complete, "propertyScanComplete", always: true, eligible.Count);
        if (context.DebugMode)
        {
            context.Log.Debug(ConsoleMessages.Get("debugPropertiesWillGetComments"));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugPropertyScanWhitelist"), whitelist.Count));
            foreach (string propertyName in eligible.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugPropertyEligible"), propertyName));
            }
        }
        else
        {
            context.Log.Debug(ConsoleMessages.Get("debugPropertiesWillGetComments"));
        }

        return new PropertyScanResult(eligible, whitelist);
    }

    public static readonly PropertyScanResult Empty = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

public sealed record PropertyScanResult(HashSet<string> EligibleProperties, HashSet<string> WhitelistProperties);
