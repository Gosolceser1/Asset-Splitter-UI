using System.Xml;
using System.Xml.Linq;

namespace AssetProcessor;

/// <summary>
/// Discovers template names from the game's <c>templates.xml</c> (after RDA extraction) and writes them to config.
/// Use <c>--update-templates</c> to refresh <c>config/01_Templates</c> from the live game data.
/// </summary>
public static class TemplateExtractor
{
    private const string Anno117GameType = "anno117";
    private const string Anno117ConfigName = "Anno117";
    private const string Anno1800ConfigName = "Anno1800";

    /// <summary>
    /// Finds <c>templates.xml</c> in <paramref name="gamePath"/>, extracts all template names,
    /// and writes them to the matching config file under <c>config/01_Templates/</c>.
    /// </summary>
    /// <returns>Number of templates written; 0 when <c>templates.xml</c> is not found or is empty.</returns>
    public static int ExtractAndUpdateTemplates(string gamePath, string gameType)
    {
        try
        {
            string templatesXml = FindTemplatesXml(gamePath);
            if (string.IsNullOrEmpty(templatesXml))
            {
                Console.WriteLine(string.Format(ConsoleMessages.Get("templateXmlNotFoundInPath"), gamePath));
                return 0;
            }

            HashSet<string> found = ExtractTemplatesFromXml(templatesXml);
            if (found.Count == 0)
            {
                Console.WriteLine(ConsoleMessages.Get("noTemplatesFound"));
                return 0;
            }

            WriteTemplatesToFile(GetConfigPath(gameType), [.. found.OrderBy(t => t)]);
            return found.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("templateExtractionFailed"), ex.Message));
            return 0;
        }
    }

    /// <summary>Searches <paramref name="gamePath"/> (and sub-directories) for <c>templates.xml</c>.</summary>
    /// <returns>Full path when found; otherwise an empty string.</returns>
    public static string FindTemplatesXml(string gamePath)
    {
        string[] wellKnownPaths =
        [
            Path.Combine(gamePath, "data", "templates.xml"),
            Path.Combine(gamePath, "templates.xml"),
            Path.Combine(gamePath, "extracted", "templates.xml"),
            Path.Combine(gamePath, "rdaextract", "templates.xml")
        ];

        foreach (string path in wellKnownPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        try
        {
            return Directory.GetFiles(gamePath, "templates.xml", SearchOption.AllDirectories).FirstOrDefault() ?? "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    /// <summary>
    /// Parses <c>templates.xml</c> and returns all unique template names discovered across
    /// <c>Template[Name]</c> attributes, <c>Template/Name</c> child elements, <c>TemplateID</c> elements,
    /// <c>AssetTemplate</c> elements, and bare text-only <c>Template</c> nodes.
    /// </summary>
    public static HashSet<string> ExtractTemplatesFromXml(string xmlPath)
    {
        try
        {
            XDocument doc = XDocument.Load(xmlPath);

            IEnumerable<string> templateAttributeNames = doc.Descendants("Template")
              .Select(template => template.Attribute("Name")?.Value ?? "");

            IEnumerable<string> templateChildNames = doc.Descendants("Template")
              .Select(template => template.Element("Name")?.Value ?? "");

            IEnumerable<string> templateIdNames = doc.Descendants("TemplateID")
              .Select(template => template.Value);

            IEnumerable<string> assetTemplateNames = doc.Descendants("AssetTemplate")
              .Select(template => template.Attribute("Name")?.Value ?? template.Value ?? "");

            IEnumerable<string> textOnlyTemplateNames = doc.Descendants("Template")
              .Where(template => !template.Elements("Name").Any() && !template.HasElements)
              .Select(template => template.Value?.Trim() ?? "");

            return
            [
                ..templateAttributeNames
                  .Union(templateChildNames)
                  .Union(templateIdNames)
                  .Union(assetTemplateNames)
                  .Union(textOnlyTemplateNames)
                  .Select(templateName => templateName.Trim())
                  .Where(templateName => templateName.Length > 0)
            ];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("templateParseFailed"), ex.Message));
            throw;
        }
    }

    /// <summary>Compares templates found in the game's <c>templates.xml</c> against the config file and prints a diff summary.</summary>
    public static void CompareTemplates(string gamePath, string gameType)
    {
        try
        {
            string templatesXml = FindTemplatesXml(gamePath);
            if (string.IsNullOrEmpty(templatesXml))
            {
                Console.WriteLine(ConsoleMessages.Get("templateXmlNotFound"));
                return;
            }

            HashSet<string> fromGame = ExtractTemplatesFromXml(templatesXml);
            string configPath = GetConfigPath(gameType);
            HashSet<string> fromConfig = ReadTemplateConfig(configPath);

            List<string> onlyInGame = [.. fromGame.Except(fromConfig).OrderBy(t => t)];
            List<string> onlyInConfig = [.. fromConfig.Except(fromGame).OrderBy(t => t)];
            int unchanged = fromGame.Intersect(fromConfig).Count();

            Console.WriteLine("");
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesInGame"), fromGame.Count));
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesInConfig"), fromConfig.Count));
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesUnchanged"), unchanged));
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesNew"), onlyInGame.Count));
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesRemoved"), onlyInConfig.Count));
            Console.WriteLine("");

            PrintDiffSection(ConsoleMessages.Get("compareTemplatesNewHeader"), "+", onlyInGame);
            PrintDiffSection(ConsoleMessages.Get("compareTemplatesRemovedHeader"), "-", onlyInConfig);

            if (onlyInGame.Count == 0 && onlyInConfig.Count == 0)
            {
                Console.WriteLine(ConsoleMessages.Get("templatesInSync"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("templateComparisonFailed"), ex.Message));
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the game's <c>templates.xml</c> contains templates
    /// that differ from or are missing from the config file.
    /// </summary>
    public static bool NeedsUpdate(string gamePath, string gameType)
    {
        try
        {
            string templatesXml = FindTemplatesXml(gamePath);
            if (string.IsNullOrEmpty(templatesXml))
            {
                return false;
            }

            HashSet<string> fromGame = ExtractTemplatesFromXml(templatesXml);
            string configPath = GetConfigPath(gameType);
            if (!File.Exists(configPath))
            {
                return true;
            }

            HashSet<string> fromConfig = ReadTemplateConfig(configPath);
            return !fromGame.SetEquals(fromConfig);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return false;
        }
    }

    private static string GetConfigPath(string gameType)
    {
        string gameName = gameType.Equals(Anno117GameType, StringComparison.OrdinalIgnoreCase)
          ? Anno117ConfigName
          : Anno1800ConfigName;

        return TemplateLoader.ResolveConfigPath("01_Templates", $"{gameName}_Templates.txt");
    }

    private static void WriteTemplatesToFile(string filePath, IReadOnlyCollection<string> templates)
    {
        try
        {
            StringBuilder content = new();
            content.AppendLine("# Auto-generated template list");
            content.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            content.AppendLine($"# Total templates: {templates.Count}");
            content.AppendLine("# Format: One template name per line");
            content.AppendLine("#");
            content.AppendLine("");

            foreach (string template in templates)
            {
                content.AppendLine(template);
            }

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            File.WriteAllText(filePath, content.ToString(), new UTF8Encoding(false));
            Console.WriteLine(string.Format(ConsoleMessages.Get("autoUpdateConfigUpdated"), templates.Count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("templateWriteFailed"), ex.Message));
            throw;
        }
    }

    private static void PrintDiffSection(string header, string prefix, List<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        Console.WriteLine(header);
        foreach (string name in entries.Take(10))
        {
            Console.WriteLine($"      {prefix} {name}");
        }

        if (entries.Count > 10)
        {
            Console.WriteLine(string.Format(ConsoleMessages.Get("compareTemplatesMore"), entries.Count - 10));
        }

        Console.WriteLine("");
    }

    private static HashSet<string> ReadTemplateConfig(string configPath)
    {
        return
        [
            ..File.ReadLines(configPath)
              .Select(line => line.Trim())
              .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
        ];
    }
}
