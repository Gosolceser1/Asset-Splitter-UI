namespace AssetProcessor;

internal sealed record AssetProcessorCommandLineOptions(
    string AssetRoot,
    string BaseOutputDir,
    string AssetLanguage,
    string CustomTemplateFile,
    string CustomFixlistFile,
    bool AssetComments,
    bool AssetFix,
    bool AssetTemplates,
    bool AssetModOpsWrap,
    bool AssetNoDefaultProperties,
    bool AssetSplitTemplates,
    bool CreateAssetMods,
    bool DebugMode,
    bool AutoTemplates,
    bool SourceExtractionOnly,
    string SingleAssetGuid,
    string ReadmeLanguage)
{
    private const string LanguageOptionPrefix = "-l:";

    public static string GetConsoleLanguage(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        foreach (string arg in args)
        {
            if (arg.StartsWith(LanguageOptionPrefix, StringComparison.Ordinal) && arg.Length > LanguageOptionPrefix.Length)
            {
                return arg[LanguageOptionPrefix.Length..].ToLowerInvariant();
            }
        }

        return "english";
    }

    public static bool IsHelpRequest(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(arg =>
            arg.Equals("-h", StringComparison.Ordinal)
            || arg.Equals("--help", StringComparison.Ordinal));
    }

    public static bool HasTemplateCommand(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Any(arg =>
          arg.Equals("--update-templates", StringComparison.Ordinal)
          || arg.Equals("--compare-templates", StringComparison.Ordinal));
    }

    public static AssetProcessorCommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 3)
        {
            throw new ArgumentException("Expected arguments: <source> <output> <language>.");
        }

        string assetLanguage = args[2].Equals("none", StringComparison.OrdinalIgnoreCase)
          ? "none"
          : NormalizeAssetLanguageFile(args[2]);

        return new AssetProcessorCommandLineOptions(
          args[0],
          args[1],
          assetLanguage,
          ParseOptionValue(args, "-u"),
          ParseOptionValue(args, "-x"),
          ContainsOption(args, "-c"),
          ContainsOption(args, "-f"),
          ContainsOption(args, "-t"),
          !ContainsOption(args, "--no-modops-wrap"),
          ContainsOption(args, "--no-default-properties"),
          ContainsOption(args, "--split-templates"),
          ContainsOption(args, "--create-asset-mods"),
          ContainsOption(args, "-d"),
          ContainsOption(args, "--auto-templates"),
          ContainsOption(args, "--source-extraction-only"),
          ParseSingleAssetGuid(args),
          ParseReadmeLanguage(args));
    }

    private static bool ContainsOption(string[] args, string option)
    {
        return args.Any(arg => arg.Equals(option, StringComparison.Ordinal));
    }

    private static string ParseSingleAssetGuid(string[] args)
    {
        string guid = ParseOptionValue(args, "-g");
        if (!string.IsNullOrEmpty(guid) && !guid.All(char.IsDigit))
        {
            throw new ArgumentException($"Invalid GUID '{guid}' - GUIDs must be numeric (e.g. 1010017).");
        }

        return guid;
    }

    private static string ParseOptionValue(string[] args, string option)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string prefixedOption = option + ":";

            if (arg.StartsWith(prefixedOption, StringComparison.Ordinal))
            {
                return arg[prefixedOption.Length..];
            }

            if (arg.Equals(option, StringComparison.Ordinal) && i + 1 < args.Length && !IsOption(args[i + 1]))
            {
                return args[i + 1];
            }
        }

        return "";
    }

    private static bool IsOption(string value)
    {
        return value.StartsWith("-", StringComparison.Ordinal);
    }

    private static string ParseReadmeLanguage(string[] args)
    {
        string value = ParseOptionValue(args, "--readme-lang");
        return string.IsNullOrWhiteSpace(value) ? "english" : value.Trim().ToLowerInvariant();
    }

    /// <summary>Accepts <c>chinese</c>, <c>texts_chinese</c>, or <c>texts_chinese.xml</c> from CLI/GUI.</summary>
    private static string NormalizeAssetLanguageFile(string value)
    {
        string language = value.Trim();
        if (!language.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            language = "texts_" + language;
        }

        if (!language.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            language += ".xml";
        }

        return language;
    }
}
