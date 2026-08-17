using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private static bool TryCreatePackage(
        PipelineContext context,
        string gameType,
        string sourceRoot,
        string outputRoot,
        string file,
        HashSet<string> usedDirectories,
        Dictionary<string, List<AssetModIndexEntry>> indexes,
        out string? writtenModAssetsPath)
    {
        writtenModAssetsPath = null;
        string relativePath = Path.GetRelativePath(sourceRoot, file);
        string[] parts = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        if (!TryReadAssetMetadata(context, file, out AssetModMetadata metadata))
        {
            context.Issues.ReportModPackageSkipped(file);
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "assetModsSkippingInvalidXml", always: true, file);
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
            string preferredModRoot = context.CreateTemplateFolders
                ? Path.Combine(outputRoot, OutputPathSafety.SanitizeSinglePathSegment(templateName, "Asset"), modFolderName)
                : Path.Combine(outputRoot, modFolderName);
            modRoot = ReserveUniqueDirectory(preferredModRoot, usedDirectories);
        }
        string assetsPath = Path.Combine(modRoot, GetAssetsRelativePath(gameType));

        OutputDirectoryManager.EnsureDirectoryExists(Path.GetDirectoryName(assetsPath));
        WriteModLoaderAssetFile(context, file, assetsPath, metadata.Guid);
        writtenModAssetsPath = assetsPath;

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
            string indexKey = context.CreateTemplateFolders
                ? OutputPathSafety.SanitizeSinglePathSegment(templateName, "Asset")
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
            XmlDocumentLoadRetry.Load(xml, file);
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
            XmlDocumentLoadRetry.Load(xml, file);
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException)
        {
            context.Issues.ReportModPackageReadFailed(file, ex.Message);
            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "assetModsReadXmlWarning", always: true, file, ex.Message);
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
}
