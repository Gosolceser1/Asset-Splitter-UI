using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private static void WriteModInfo(string modRoot, string gameType, string modId, AssetModMetadata metadata, string templateName, PipelineContext context)
    {
        bool isAnno117 = gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase);
        string modName = metadata.HasDistinctInternalName
            ? $"{metadata.Guid} - {metadata.DisplayName} ({metadata.InternalName})"
            : $"{metadata.Guid} - {metadata.DisplayName}";

        string descriptionTemplate = ConsoleMessages.Get("modDescriptionTemplate");
        string description = string.Format(descriptionTemplate, templateName);
        string gameBuild = GeneratedXmlFootprint.BuildGameBuildDescription(context);
        if (!string.IsNullOrWhiteSpace(gameBuild))
        {
            description += " " + string.Format(ConsoleMessages.Get("modDescriptionGameBuildSuffix"), gameBuild);
        }

        description += " " + string.Format(ConsoleMessages.Get("modDescriptionToolSuffix"), ModReadmeWriter.ProjectHomeUrl);

        string categoryName = ConsoleMessages.Get("modCategoryName");

        string assetLanguage = NormalizeAssetLanguage(context.AssetLanguage);
        string languageKey = string.IsNullOrWhiteSpace(assetLanguage) || assetLanguage == "none"
            ? "English"
            : GameLanguageKeys.GetAnnoKeyOrEnglish(assetLanguage);

        var modNames = new SortedDictionary<string, string> { [languageKey] = modName };
        var descriptions = new SortedDictionary<string, string> { [languageKey] = description };
        var categories = new SortedDictionary<string, string> { [languageKey] = categoryName };

        if (languageKey != "English")
        {
            string localizedTemplate = ConsoleMessages.GetForLanguage("modDescriptionTemplate", assetLanguage);
            if (!string.IsNullOrWhiteSpace(localizedTemplate))
            {
                descriptions[languageKey] = string.Format(localizedTemplate, templateName);
                if (!string.IsNullOrWhiteSpace(gameBuild))
                {
                    descriptions[languageKey] += " " + string.Format(ConsoleMessages.GetForLanguage("modDescriptionGameBuildSuffix", assetLanguage), gameBuild);
                }

                descriptions[languageKey] += " " + string.Format(
                    ConsoleMessages.GetForLanguage("modDescriptionToolSuffix", assetLanguage),
                    ModReadmeWriter.ProjectHomeUrl);
            }

            string localizedCategory = ConsoleMessages.GetForLanguage("modCategoryName", assetLanguage);
            if (!string.IsNullOrWhiteSpace(localizedCategory))
            {
                categories[languageKey] = localizedCategory;
            }
        }

        var modInfo = new SortedDictionary<string, object?>
        {
            ["Anno"] = isAnno117 ? 8 : 7,
            ["Version"] = "1.0.0",
            ["ModID"] = modId,
            ["ModName"] = modNames,
            ["Description"] = descriptions,
            ["Category"] = categories
        };

        if (isAnno117)
        {
            modInfo["Difficulty"] = "unchanged";
            modInfo["GameSetup"] = new SortedDictionary<string, object?>
            {
                ["RequiresNewGame"] = false,
                ["SafeToRemove"] = true,
                ["Multiplayer"] = true,
                ["Campaign"] = true
            };
            modInfo["Dependencies"] = new SortedDictionary<string, string[]>
            {
                ["Require"] = [],
                ["Optional"] = [],
                ["LoadAfter"] = [],
                ["Deprecate"] = [],
                ["Incompatible"] = []
            };
        }
        else
        {
            modInfo["ModDependencies"] = Array.Empty<string>();
            modInfo["OptionalDependencies"] = Array.Empty<string>();
            modInfo["LoadAfterIds"] = Array.Empty<string>();
            modInfo["DeprecateIds"] = Array.Empty<string>();
            modInfo["IncompatibleIds"] = Array.Empty<string>();
        }

        string json = JsonSerializer.Serialize(modInfo, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        File.WriteAllText(Path.Combine(modRoot, "modinfo.json"), json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string NormalizeAssetLanguage(string? value)
    {
        string language = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        language = Path.GetFileNameWithoutExtension(language);

        if (language.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            language = language[6..];
        }

        if (language.StartsWith("console_", StringComparison.OrdinalIgnoreCase))
        {
            language = language[8..];
        }

        language = language.Replace('-', '_');

        return language switch
        {
            "en" => "english",
            "de" => "german",
            "fr" => "french",
            "it" => "italian",
            "es" => "spanish",
            "pl" => "polish",
            "ru" => "russian",
            "ja" => "japanese",
            "jp" => "japanese",
            "ko" => "korean",
            "kr" => "korean",
            "cs" => "czech",
            "pt" => "portuguese",
            "pt_br" => "brazilian",
            "br" => "brazilian",
            "es_mx" => "mexican",
            "mx" => "mexican",
            "zh" or "zh_cn" or "cn" => "chinese",
            "zh_tw" or "tw" => "tchinese",
            _ => language
        };
    }

    private static void AddIndexEntry(
        Dictionary<string, List<AssetModIndexEntry>> indexes,
        string templateFolder,
        AssetModMetadata metadata,
        string modFolderName)
    {
        if (!indexes.TryGetValue(templateFolder, out List<AssetModIndexEntry>? entries))
        {
            entries = [];
            indexes[templateFolder] = entries;
        }

        entries.Add(new AssetModIndexEntry(metadata.Guid, metadata.DisplayName, metadata.InternalName, metadata.PathHint, modFolderName));
    }

    private static void WriteTemplateIndexes(string outputRoot, Dictionary<string, List<AssetModIndexEntry>> indexes, string readmeLanguage)
    {
        foreach (var (templateFolder, entries) in indexes)
        {
            string indexPath = string.IsNullOrEmpty(templateFolder)
                ? Path.Combine(outputRoot, "INDEX.md")
                : Path.Combine(outputRoot, templateFolder, "INDEX.md");
            IReadOnlyList<string> tableHeader = ModReadmeWriter.GetIndexTableHeader(readmeLanguage);
            List<string> lines =
            [
                ModReadmeWriter.GetIndexTitle(string.IsNullOrEmpty(templateFolder) ? "Asset mods" : templateFolder, readmeLanguage),
                "",
                ModReadmeWriter.GetIndexBrowseLine(readmeLanguage),
                "",
                $"| {tableHeader[0]} | {tableHeader[1]} | {tableHeader[2]} | {tableHeader[3]} | {tableHeader[4]} |",
                "|---|---|---|---|---|"
            ];

            foreach (AssetModIndexEntry entry in entries.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Guid, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"| {EscapeMarkdownTable(entry.Guid)} | {EscapeMarkdownTable(entry.DisplayName)} | {EscapeMarkdownTable(DisplayValue(entry.InternalName))} | {EscapeMarkdownTable(DisplayValue(entry.PathHint))} | {EscapeMarkdownTable(entry.ModFolderName)} |");
            }

            File.WriteAllLines(indexPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static string ReadPathHint(XmlDocument xml)
    {
        string[] xpaths =
        [
            "//FilePath1x1Preview",
            "//TileSetCfgFolder",
            "//CanalTileSetCfgFolder_Deprecated",
            "//Filename",
            "//IconFilename"
        ];

        foreach (string xpath in xpaths)
        {
            string value = xml.SelectSingleNode(xpath)?.InnerText.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                return ShortenPathHint(value);
            }
        }

        return "";
    }

    private static string ShortenPathHint(string value)
    {
        string normalized = value.Replace('\\', '/').TrimEnd('/');
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", parts.TakeLast(Math.Min(3, parts.Length)));
    }

    private static string DisplayValue(string value) => string.IsNullOrWhiteSpace(value) ? "n/a" : value.Trim();

    private static string EscapeMarkdownTable(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
