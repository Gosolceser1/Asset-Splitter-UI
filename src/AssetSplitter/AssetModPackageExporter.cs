using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private const int MaxFolderNameLength = 90;
    private const string FlatModIndexKey = "";

    public static AssetModPackageExportResult Export(PipelineContext context, string gameType)
    {
        string sourceRoot = context.AssetOut;
        string outputRoot = context.IsSingleAssetMode
            ? context.SingleAssetModOutputRoot
            : context.AssetModOutputRoot;

        if (!Directory.Exists(sourceRoot))
        {
            return new AssetModPackageExportResult(outputRoot, 0, 0);
        }

        if (!OutputDirectoryManager.TryPrepareModOutputDirectory(context))
        {
            return new AssetModPackageExportResult(outputRoot, 0, 0);
        }

        string[] files = GetFilesToPackage(context, sourceRoot)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        context.Log.Write("PHASE", ConsoleMessages.Get("phase8AssetModPackages"), always: true);
        if (context.IsSingleAssetMode)
        {
            context.Log.Write("MODS", string.Format(ConsoleMessages.Get("assetModsCreatingSingle"), outputRoot), always: true);
        }
        else
        {
            context.Log.Write("MODS", string.Format(ConsoleMessages.Get("assetModsCreatingMany"), outputRoot), always: true);
        }

        int created = 0;
        int skipped = 0;
        var usedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexes = new Dictionary<string, List<AssetModIndexEntry>>(StringComparer.OrdinalIgnoreCase);

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugModExportSource"), sourceRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugModExportTarget"), outputRoot));
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetModsProcessingFiles"), files.Length.ToString("N0")));

        foreach (string file in files)
        {
            bool packageCreated;
            try
            {
                packageCreated = TryCreatePackage(context, gameType, sourceRoot, outputRoot, file, usedDirectories, indexes);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                context.Issues.ReportModPackageReadFailed(file, ex.Message);
                context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("assetModsReadXmlWarning"), file, ex.Message), always: true);
                packageCreated = false;
            }

            if (!packageCreated)
            {
                if (context.DebugMode)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetModsSkippedInvalidMetadata"), Path.GetFileName(file)));
                }

                context.DebugStats.RecordModPackageSkipped();
                skipped++;
                continue;
            }

            created++;
            context.DebugStats.RecordModPackageCreated();
            if (ShouldReportModProgressIntermediate(context, created, files.Length))
            {
                string? templateName = context.DebugMode
                    ? null
                    : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(file);
                string modProgress = context.DebugMode
                    ? ConsoleMessages.Get("assetModsProgress")
                    : AssetProgressFormatter.FromAssetFileStem("Creating mod", Path.GetFileNameWithoutExtension(file), templateName);
                context.ProgressReporter.OutputFixer(modProgress, created.ToString(), files.Length.ToString());
            }
        }

        if (files.Length > 0)
        {
            context.ProgressReporter.OutputFixer(ConsoleMessages.Get("assetModsProgress"), created.ToString(), files.Length.ToString());
        }

        if (created > 0)
        {
            ModReadmeWriter.WriteModdingGuide(outputRoot, gameType, context);
        }

        if (!context.IsSingleAssetMode)
        {
            WriteTemplateIndexes(outputRoot, indexes, context.ReadmeLanguage);
            ModReadmeWriter.WriteExportSummary(outputRoot, gameType, created, skipped, singleAssetMode: false, context);
        }
        else if (created > 0)
        {
            ModReadmeWriter.WriteExportSummary(outputRoot, gameType, created, skipped, singleAssetMode: true, context);
        }
        if (context.DebugMode)
        {
            context.DebugStats.WriteModPackageSummary(context.Log);
        }

        if (context.IsSingleAssetMode)
        {
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("assetModsCreatedSingle"), outputRoot), always: true);
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("assetModsSingleXmlOutput"), context.SingleAssetOutputRoot), always: true);
        }
        else
        {
            context.Log.Write("OK", string.Format(ConsoleMessages.Get("assetModsCreatedSummary"), created.ToString("N0"), skipped.ToString("N0")), always: true);
            context.Log.Write("INFO", string.Format(ConsoleMessages.Get("assetModsOutputFolder"), outputRoot), always: true);
        }
        return new AssetModPackageExportResult(outputRoot, created, skipped);
    }

    private static bool ShouldReportModProgressIntermediate(PipelineContext context, int created, int total) =>
        DeveloperTrace.ShouldReportProgress(context, created, total, normalInterval: 200);

    private static IEnumerable<string> GetFilesToPackage(PipelineContext context, string sourceRoot)
    {
        if (!context.IsSingleAssetMode)
        {
            return Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.AllDirectories);
        }

        string[] topLevelFiles = Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.TopDirectoryOnly).ToArray();
        return topLevelFiles.Length > 0
            ? topLevelFiles
            : Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.AllDirectories);
    }

    private static bool TryCreatePackage(
        PipelineContext context,
        string gameType,
        string sourceRoot,
        string outputRoot,
        string file,
        HashSet<string> usedDirectories,
        Dictionary<string, List<AssetModIndexEntry>> indexes)
    {
        string relativePath = Path.GetRelativePath(sourceRoot, file);
        string[] parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        if (!TryReadAssetMetadata(context, file, out AssetModMetadata metadata))
        {
            context.Issues.ReportModPackageSkipped(file);
            context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("assetModsSkippingInvalidXml"), file), always: true);
            return false;
        }

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetModsCreatingPackage"), Path.GetFileName(file)));
        }

        string templateName = ResolveTemplateFolderName(context, parts, file);

        string modFolderName = CreateModFolderName(metadata);
        string modRoot;
        if (context.IsSingleAssetMode)
        {
            modRoot = outputRoot;
        }
        else
        {
            string preferredModRoot = context.AssetTemplates
                ? Path.Combine(outputRoot, AssetTextSanitizer.SanitizeFileNamePart(templateName, MaxFolderNameLength), modFolderName)
                : Path.Combine(outputRoot, modFolderName);
            modRoot = ReserveUniqueDirectory(preferredModRoot, usedDirectories);
        }
        string assetsPath = Path.Combine(modRoot, GetAssetsRelativePath(gameType));

        OutputDirectoryManager.EnsureDirectoryExists(Path.GetDirectoryName(assetsPath));
        WriteModLoaderAssetFile(context, file, assetsPath, metadata.Guid);

        string modId = $"asset-splitter-{gameType}-{NormalizeIdPart(metadata.Guid)}-{Slugify(metadata.DisplayName, MaxFolderNameLength)}";
        bool shouldWritePackageMetadata = !context.IsSingleAssetMode
            || !File.Exists(Path.Combine(modRoot, "modinfo.json"));
        if (shouldWritePackageMetadata)
        {
            WriteModInfo(modRoot, gameType, modId, metadata, templateName, context);
            ModReadmeWriter.WriteShortReadme(modRoot, outputRoot, gameType, metadata, templateName, context.IsSingleAssetMode, context);
        }
        if (!context.IsSingleAssetMode)
        {
            string indexKey = context.AssetTemplates
                ? AssetTextSanitizer.SanitizeFileNamePart(templateName, MaxFolderNameLength)
                : FlatModIndexKey;
            AddIndexEntry(indexes, indexKey, metadata, Path.GetFileName(modRoot));
        }

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(
                ConsoleMessages.Get("debugAssetModsPackageCreated"),
                Path.GetFileName(modRoot),
                metadata.Guid,
                metadata.DisplayName));
        }

        return true;
    }

    private static string ReadTemplateName(string file)
    {
        try
        {
            var xml = new XmlDocument { PreserveWhitespace = false };
            xml.Load(file);
            return xml.SelectSingleNode("//Asset/Template")?.InnerText.Trim() ?? "";
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static string ResolveTemplateFolderName(PipelineContext context, string[] relativeParts, string file)
    {
        string stagingFolder = context.AppSettingsConfig?.Settings?.OutputStructure?.BaseassetFolder ?? OutputStructureSettings.DefaultBaseAssetFolder;
        string templateName = relativeParts.Length >= 2 ? relativeParts[0] : ReadTemplateName(file);

        if (string.IsNullOrWhiteSpace(templateName)
            || templateName.Equals(stagingFolder, StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("BaseAssetGUID", StringComparison.OrdinalIgnoreCase))
        {
            string fromXml = ReadTemplateName(file);
            if (!string.IsNullOrWhiteSpace(fromXml))
            {
                templateName = fromXml;
            }
        }

        return string.IsNullOrWhiteSpace(templateName) ? "Asset" : templateName;
    }

    private static bool TryReadAssetMetadata(PipelineContext context, string file, out AssetModMetadata metadata)
    {
        metadata = new AssetModMetadata("", "", "", "", "");
        string guid = "";
        string displayName = "";

        Match fileNameMatch = AssetFileNameRegex().Match(Path.GetFileName(file));
        if (fileNameMatch.Success)
        {
            guid = fileNameMatch.Groups["guid"].Value;
            displayName = fileNameMatch.Groups["name"].Value;
        }

        var xml = new XmlDocument { PreserveWhitespace = true };
        try
        {
            xml.Load(file);
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            context.Issues.ReportModPackageReadFailed(file, ex.Message);
            context.Log.Write("WARNING", string.Format(ConsoleMessages.Get("assetModsReadXmlWarning"), file, ex.Message), always: true);
            return false;
        }

        XmlNode? modOp = xml.SelectSingleNode("/ModOps/ModOp");
        XmlNode? assetNode = modOp?.SelectSingleNode("Asset") ?? xml.SelectSingleNode("//Asset");
        if (assetNode is null)
        {
            return false;
        }

        if (modOp is not null)
        {
            guid = modOp.Attributes?["GUID"]?.Value.Trim() ?? guid;
        }

        if (string.IsNullOrWhiteSpace(guid))
        {
            guid = assetNode.SelectSingleNode(".//Values/Standard/GUID")?.InnerText.Trim() ?? guid;
        }

        if (string.IsNullOrWhiteSpace(guid))
        {
            return false;
        }

        string internalName = xml.SelectSingleNode("//Standard/Name")?.InnerText.Trim() ?? "";
        string assetLang = NormalizeAssetLanguage(context.AssetLanguage);
        if (!string.IsNullOrWhiteSpace(assetLang)
            && assetLang != "none"
            && assetLang != "english")
        {
            string? translatedInternal = context.AssetNames.TryGetValue(guid, out string? an) ? an : null;
            if (!string.IsNullOrWhiteSpace(translatedInternal))
            {
                internalName = translatedInternal;
            }
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = string.IsNullOrWhiteSpace(internalName) ? "Asset" : internalName;
        }

        string pathHint = ReadPathHint(xml);
        metadata = new AssetModMetadata(guid, displayName.Trim(), internalName, pathHint, file);
        return true;
    }

    private static string CreateModFolderName(AssetModMetadata metadata)
    {
        string safeGuid = NormalizeIdPart(metadata.Guid);
        string cleanDisplay = SanitizePathPart(metadata.DisplayName, MaxFolderNameLength);
        string folderName = $"{safeGuid} - {cleanDisplay}";

        if (!string.IsNullOrWhiteSpace(metadata.InternalName))
        {
            string cleanInternal = SanitizePathPart(metadata.InternalName, MaxFolderNameLength);
            if (!cleanInternal.Equals(cleanDisplay, StringComparison.OrdinalIgnoreCase))
            {
                folderName += $" - {cleanInternal}";
            }
        }

        return folderName.Length <= MaxFolderNameLength ? folderName : folderName[..MaxFolderNameLength].TrimEnd(' ', '-');
    }

    private static string ReserveUniqueDirectory(string preferredPath, HashSet<string> usedDirectories)
    {
        string candidate = preferredPath;
        string? parent = Path.GetDirectoryName(preferredPath);
        string baseName = Path.GetFileName(preferredPath);
        int suffix = 2;
        while (!usedDirectories.Add(candidate) || Directory.Exists(candidate))
        {
            string suffixText = "-" + suffix;
            string trimmedBaseName = baseName.Length + suffixText.Length <= MaxFolderNameLength
                ? baseName
                : baseName[..(MaxFolderNameLength - suffixText.Length)].TrimEnd('-');
            candidate = Path.Combine(parent ?? "", trimmedBaseName + suffixText);
            suffix++;
        }

        return candidate;
    }

    private static string GetAssetsRelativePath(string gameType)
    {
        return gameType.Equals("anno117", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine("data", "base", "config", "export", "assets.xml")
            : Path.Combine("data", "config", "export", "main", "asset", "assets.xml");
    }

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

        string categoryName = ConsoleMessages.Get("modCategoryName");

        string assetLanguage = NormalizeAssetLanguage(context.AssetLanguage);
        string languageKey = !string.IsNullOrWhiteSpace(assetLanguage) && assetLanguage != "none"
            && GameLanguageToAnnoKey.TryGetValue(assetLanguage, out string? lk) ? lk : "English";

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

    private static readonly Dictionary<string, string> GameLanguageToAnnoKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["english"] = "English",
        ["german"] = "German",
        ["french"] = "French",
        ["italian"] = "Italian",
        ["spanish"] = "Spanish",
        ["polish"] = "Polish",
        ["russian"] = "Russian",
        ["japanese"] = "Japanese",
        ["korean"] = "Korean",
        ["chinese"] = "Chinese (Simplified)",
        ["tchinese"] = "Chinese (Traditional)",
        ["czech"] = "Czech",
        ["portuguese"] = "Portuguese",
        ["brazilian"] = "Brazilian Portuguese",
        ["mexican"] = "Mexican Spanish",
        ["simplified_chinese"] = "Chinese (Simplified)",
        ["traditional_chinese"] = "Chinese (Traditional)"
    };

    private static string GetAnnoLanguageKey(string assetLanguage) =>
        GameLanguageToAnnoKey.TryGetValue(assetLanguage, out string? key) ? key : "English";

    private static string ResolveTranslatedAssetName(PipelineContext context, string guid, string fallbackName)
    {
        if (context.Translator.TryGetValue(guid, out string? translated) && !string.IsNullOrWhiteSpace(translated))
        {
            return $"{guid} - {translated}";
        }

        if (context.AssetNames.TryGetValue(guid, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            return $"{guid} - {name}";
        }

        return fallbackName;
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

    private static string CreateDescription(string templateName, AssetModMetadata metadata)
    {
        string detail = metadata.HasDistinctInternalName
            ? $" Internal name: {metadata.InternalName}."
            : "";

        return $"Generated asset mod for template '{templateName}'.{detail}";
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

    private static string EscapeXml(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);

    private static void WriteModLoaderAssetFile(PipelineContext context, string sourceFile, string destinationPath, string guid)
    {
        XmlDocument modDocument = CreateModLoaderAssetDocument(sourceFile, guid);
        OutputDirectoryManager.EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

        if (!File.Exists(destinationPath))
        {
            GeneratedXmlFootprint.Save(modDocument, destinationPath, context);
            return;
        }

        var existingDocument = new XmlDocument { PreserveWhitespace = true };
        existingDocument.Load(destinationPath);
        XmlNode? existingRoot = existingDocument.SelectSingleNode("/ModOps") ?? throw new InvalidOperationException("Existing mod assets.xml is missing ModOps root: " + destinationPath);
        XmlNodeList? modOps = modDocument.SelectNodes("/ModOps/ModOp");
        if (modOps is null || modOps.Count == 0)
        {
            throw new InvalidOperationException("Source asset did not produce ModOp entries: " + sourceFile);
        }

        foreach (XmlNode modOp in modOps)
        {
            existingRoot.AppendChild(existingDocument.ImportNode(modOp, true));
        }

        GeneratedXmlFootprint.Save(existingDocument, destinationPath, context);
    }

    private static XmlDocument CreateModLoaderAssetDocument(string sourceFile, string guid)
    {
        var xml = new XmlDocument { PreserveWhitespace = true };
        xml.Load(sourceFile);

        if (xml.SelectSingleNode("/ModOps/ModOp") is not null)
        {
            return xml;
        }

        XmlNode? assetNode = xml.SelectSingleNode("//Asset")
            ?? throw new InvalidOperationException("Asset node missing in " + sourceFile);

        if (string.IsNullOrWhiteSpace(guid))
        {
            guid = assetNode.SelectSingleNode(".//Values/Standard/GUID")?.InnerText.Trim() ?? "";
        }

        return CreateModOpsDocument(assetNode, guid);
    }

    private static XmlDocument CreateModOpsDocument(XmlNode assetNode, string guid)
    {
        XmlDocument document = new();
        XmlElement modOpsElement = document.CreateElement("ModOps");
        document.AppendChild(modOpsElement);

        XmlElement modOpElement = document.CreateElement("ModOp");
        modOpElement.SetAttribute("GUID", guid);
        modOpElement.SetAttribute("Type", "Replace");
        modOpElement.SetAttribute("Path", "/");
        modOpsElement.AppendChild(modOpElement);
        modOpElement.AppendChild(document.ImportNode(assetNode, true));
        return document;
    }

    private static string NormalizeIdPart(string value)
    {
        string trimmed = value.Trim();
        return trimmed.StartsWith("-", StringComparison.Ordinal) ? "minus-" + trimmed[1..] : Slugify(trimmed, 40);
    }

    private static string Slugify(string value, int maxLength)
    {
        var builder = new StringBuilder(value.Length);
        bool previousWasDash = false;

        foreach (char c in value.ToLowerInvariant())
        {
            bool allowed = char.IsAsciiLetterOrDigit(c);
            if (allowed)
            {
                builder.Append(c);
                previousWasDash = false;
            }
            else if (!previousWasDash)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        string slug = builder.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "asset";
        }

        return slug.Length <= maxLength ? slug : slug[..maxLength].TrimEnd('-');
    }

    private static string SanitizePathPart(string value, int maxLength)
    {
        static bool IsAllowed(char c) =>
            c >= ' ' && c is not ('<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (char c in value.Trim())
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            if (IsAllowed(c))
            {
                builder.Append(c);
            }
        }

        string result = builder.ToString().Trim('.', ' ');
        return string.IsNullOrEmpty(result) ? "asset" : result;
    }

    [GeneratedRegex("^(?<guid>-?\\d+) - \\[ (?<name>.*) \\]\\.xml$", RegexOptions.CultureInvariant)]
    private static partial Regex AssetFileNameRegex();
}

public sealed record AssetModPackageExportResult(string OutputRoot, int CreatedCount, int SkippedCount);

internal sealed record AssetModMetadata(
    string Guid,
    string DisplayName,
    string InternalName,
    string PathHint,
    string SourceFile)
{
    public bool HasDistinctInternalName =>
        !string.IsNullOrWhiteSpace(InternalName) &&
        !InternalName.Equals(DisplayName, StringComparison.OrdinalIgnoreCase);
}

internal sealed record AssetModIndexEntry(
    string Guid,
    string DisplayName,
    string InternalName,
    string PathHint,
    string ModFolderName);
