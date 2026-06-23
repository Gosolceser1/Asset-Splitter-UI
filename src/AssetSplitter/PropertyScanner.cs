using System.Xml;

namespace AssetProcessor;

public static class PropertyScanner
{
    public static PropertyScanResult Scan(PipelineContext context)
    {
        string filename = context.SourceXmlFolder + "properties.xml";
        XmlDocument xmlDocument = new();
        xmlDocument.Load(filename);
        XmlNodeList? xmlNodeList = xmlDocument.DocumentElement?.SelectNodes("//*");
        HashSet<string> eligible = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> whitelist = new(StringComparer.OrdinalIgnoreCase);

        var propertyExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "None", "Deuteranopia", "Protanopia", "Tritanopia", "ColorMode"
        };

        context.Log.Write("ANALYZE", ConsoleMessages.Get("propertyScanAnalyze"));
        context.Log.Write("INFO", ConsoleMessages.Get("propertyScanInfo"));

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

        context.Log.Write("COMPLETE", string.Format(ConsoleMessages.Get("propertyScanComplete"), eligible.Count));
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
