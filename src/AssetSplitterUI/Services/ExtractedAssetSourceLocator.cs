namespace AssetSplitterUI.Services;

/// <summary>Finds already-extracted source XML folders and game languages in an output directory.</summary>
public static class ExtractedAssetSourceLocator
{
    private const string Anno1800DisplayName = "Anno1800";
    private const string Anno117DisplayName = "Anno117";
    private const string Anno1800GameType = "anno1800";
    private const string Anno117GameType = "anno117";

    private static readonly string[] Anno1800RequiredSourceFiles =
    [
        "assets.xml",
        "properties.xml",
        "properties-toolone.xml",
        "templates.xml",
        "datasets.xml"
    ];

    private static readonly string[] Anno117RequiredSourceFiles =
    [
        "assets.xml",
        "properties.xml",
        "properties-meta.xml",
        "templates.xml",
        "datasets.xml",
        "audio_generated.xml"
    ];

    /// <summary>Returns the display game type when required extracted source XML files already exist; otherwise an empty string.</summary>
    public static string DetectGameType(string? outputPath, string? gamePath)
    {
        string basePath = GetSearchBasePath(outputPath, gamePath);
        if (string.IsNullOrEmpty(basePath))
        {
            return "";
        }

        string? preferredGameType = DetectPreferredGameTypeFromPath(gamePath);
        if (IsAnno117(preferredGameType))
        {
            return FindCompleteSourceFolder(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles) is not null
                ? Anno117DisplayName
                : "";
        }

        if (IsAnno1800(preferredGameType))
        {
            return FindCompleteSourceFolder(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles) is not null
                ? Anno1800DisplayName
                : "";
        }

        if (FindCompleteSourceFolder(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles) is not null)
        {
            return Anno117DisplayName;
        }

        if (FindCompleteSourceFolder(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles) is not null)
        {
            return Anno1800DisplayName;
        }

        return "";
    }

    /// <summary>Returns sorted game languages discovered from <c>texts_*.xml</c> files.</summary>
    public static IReadOnlyList<string> FindAvailableLanguages(string? outputPath, string? gamePath, string? selectedGameType)
    {
        HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);

        foreach (string searchPath in GetLanguageSearchPaths(outputPath, gamePath, selectedGameType))
        {
            if (!Directory.Exists(searchPath))
            {
                continue;
            }

            foreach (string file in EnumerateLanguageFiles(searchPath))
            {
                string? language = TryGetLanguageName(file);
                if (!string.IsNullOrEmpty(language))
                {
                    languages.Add(language);
                }
            }
        }

        return [.. languages.OrderBy(language => language)];
    }

    /// <summary>Returns source XML folders to scan, scoped to the selected game when known.</summary>
    public static IReadOnlyList<string> GetLanguageSearchPaths(string? outputPath, string? gamePath, string? selectedGameType)
    {
        string basePath = GetSearchBasePath(outputPath, gamePath);
        if (string.IsNullOrEmpty(basePath))
        {
            return [];
        }

        selectedGameType = string.IsNullOrWhiteSpace(selectedGameType)
            ? DetectPreferredGameTypeFromPath(gamePath)
            : selectedGameType;

        if (IsAnno1800(selectedGameType))
        {
            return GetSourceFolderCandidates(basePath, Anno1800DisplayName, "source_xml_anno1800");
        }

        if (IsAnno117(selectedGameType))
        {
            return GetSourceFolderCandidates(basePath, Anno117DisplayName, "source_xml_anno117");
        }

        return
        [
            ..GetSourceFolderCandidates(basePath, Anno1800DisplayName, "source_xml_anno1800"),
            ..GetSourceFolderCandidates(basePath, Anno117DisplayName, "source_xml_anno117")
        ];
    }

    public static string? FindAssetsXml(string? outputPath, string? gamePath, string? selectedGameType)
    {
        string basePath = GetSearchBasePath(outputPath, gamePath);
        if (string.IsNullOrEmpty(basePath))
            return null;

        selectedGameType = string.IsNullOrWhiteSpace(selectedGameType)
            ? DetectPreferredGameTypeFromPath(gamePath)
            : selectedGameType;

        IReadOnlyList<string> candidates = IsAnno1800(selectedGameType)
            ? GetCompleteAssetFileCandidates(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles)
            : IsAnno117(selectedGameType)
                ? GetCompleteAssetFileCandidates(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles)
                :
                [
                    ..GetCompleteAssetFileCandidates(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles),
                    ..GetCompleteAssetFileCandidates(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles)
                ];

        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool HasCompleteSourceXml(string? outputPath, string? gamePath, string? selectedGameType)
    {
        string basePath = GetSearchBasePath(outputPath, gamePath);
        if (string.IsNullOrEmpty(basePath))
            return false;

        selectedGameType = string.IsNullOrWhiteSpace(selectedGameType)
            ? DetectPreferredGameTypeFromPath(gamePath)
            : selectedGameType;

        if (IsAnno1800(selectedGameType))
            return FindCompleteSourceFolder(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles) is not null;

        if (IsAnno117(selectedGameType))
            return FindCompleteSourceFolder(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles) is not null;

        return FindCompleteSourceFolder(basePath, Anno117DisplayName, "source_xml_anno117", Anno117RequiredSourceFiles) is not null
            || FindCompleteSourceFolder(basePath, Anno1800DisplayName, "source_xml_anno1800", Anno1800RequiredSourceFiles) is not null;
    }

    private static bool IsAnno1800(string? gameType) =>
        string.Equals(gameType, Anno1800GameType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(gameType, Anno1800DisplayName, StringComparison.OrdinalIgnoreCase);

    private static bool IsAnno117(string? gameType) =>
        string.Equals(gameType, Anno117GameType, StringComparison.OrdinalIgnoreCase)
        || string.Equals(gameType, Anno117DisplayName, StringComparison.OrdinalIgnoreCase);

    private static string? DetectPreferredGameTypeFromPath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return null;
        }

        string directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(gamePath));
        if (directoryName.Contains("Anno 117", StringComparison.OrdinalIgnoreCase)
            || directoryName.Contains("Pax Romana", StringComparison.OrdinalIgnoreCase)
            || directoryName.Contains("Anno117", StringComparison.OrdinalIgnoreCase))
        {
            return Anno117GameType;
        }

        if (directoryName.Contains("Anno 1800", StringComparison.OrdinalIgnoreCase)
            || directoryName.Contains("Anno1800", StringComparison.OrdinalIgnoreCase))
        {
            return Anno1800GameType;
        }

        string maindataPath = Path.Combine(gamePath, "maindata");
        if (File.Exists(Path.Combine(maindataPath, "config.rda"))
            || File.Exists(Path.Combine(maindataPath, "shared_configs.rda")))
        {
            return Anno117GameType;
        }

        return Directory.Exists(maindataPath) ? Anno1800GameType : null;
    }

    private static string GetSearchBasePath(string? outputPath, string? gamePath) =>
        !string.IsNullOrWhiteSpace(outputPath) ? outputPath : gamePath ?? "";

    private static IReadOnlyList<string> GetSourceFolderCandidates(string basePath, string gameFolder, string sourceFolder) =>
    [
        Path.Combine(basePath, "AnnoAssets", gameFolder, sourceFolder),
        Path.Combine(basePath, gameFolder, sourceFolder),
        Path.Combine(basePath, sourceFolder)
    ];

    private static IReadOnlyList<string> GetCompleteAssetFileCandidates(string basePath, string gameFolder, string sourceFolder, IReadOnlyList<string> requiredFiles) =>
    [
        ..GetSourceFolderCandidates(basePath, gameFolder, sourceFolder)
            .Where(folder => HasRequiredFiles(folder, requiredFiles))
            .Select(folder => Path.Combine(folder, "assets.xml"))
    ];

    private static string? FindCompleteSourceFolder(string basePath, string gameFolder, string sourceFolder, IReadOnlyList<string> requiredFiles) =>
        GetSourceFolderCandidates(basePath, gameFolder, sourceFolder)
            .FirstOrDefault(folder => HasRequiredFiles(folder, requiredFiles));

    private static bool HasRequiredFiles(string folder, IReadOnlyList<string> requiredFiles) =>
        Directory.Exists(folder)
        && requiredFiles.All(fileName => File.Exists(Path.Combine(folder, fileName)))
        && EnumerateLanguageFiles(folder).Any();

    private static IEnumerable<string> EnumerateLanguageFiles(string searchPath)
    {
        try
        {
            return Directory.EnumerateFiles(searchPath, "texts_*.xml").ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UILogger.Warning(nameof(ExtractedAssetSourceLocator), "Failed to enumerate language files");
            UILogger.Debug(nameof(ExtractedAssetSourceLocator), ex);
            return [];
        }
    }

    private static string? TryGetLanguageName(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (!fileName.StartsWith("texts_", StringComparison.OrdinalIgnoreCase) || fileName.Length <= "texts_".Length)
        {
            return null;
        }

        string language = fileName["texts_".Length..];
        return char.ToUpperInvariant(language[0]) + language[1..];
    }
}
