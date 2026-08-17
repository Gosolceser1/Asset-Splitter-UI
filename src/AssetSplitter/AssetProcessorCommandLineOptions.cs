namespace AssetProcessor;

internal sealed record AssetProcessorCommandLineOptions(
    string AssetRoot,
    string BaseOutputDir,
    string AssetLanguage,
    string CustomTemplateFile,
    string CustomFixlistFile,
    bool AddComments,
    bool FixDependencies,
    bool CreateTemplateFolders,
    bool ModOpsWrap,
    bool NoDefaultProperties,
    bool SplitTemplates,
    bool CreateAssetMods,
    bool DebugMode,
    bool AutoTemplates,
    bool SourceExtractionOnly,
    bool ForceFullRebuild,
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
          ParseConfigFileOption(args, "-u"),
          ParseConfigFileOption(args, "-x"),
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
          ContainsOption(args, "--force-full-rebuild"),
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return "english";
        }

        string language = value.Trim().ToLowerInvariant();
        if (language.Length == 0
            || language.Contains('.', StringComparison.Ordinal)
            || language.Contains('/', StringComparison.Ordinal)
            || language.Contains('\\', StringComparison.Ordinal)
            || language.Contains("..", StringComparison.Ordinal)
            || !language.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException(
                $"Invalid readme language '{value}'. Language stem must match [a-zA-Z0-9_] only.");
        }

        return language;
    }

    private static string ParseConfigFileOption(string[] args, string option)
    {
        string value = ParseOptionValue(args, option);
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (Path.IsPathRooted(value)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || !OutputPathSafety.IsSafeSinglePathSegment(value))
        {
            throw new ArgumentException(
                $"Invalid {option} file name '{value}'. Use a config file name without path separators.");
        }

        return value;
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

        const string prefix = "texts_";
        const string suffix = ".xml";
        if (language.Length <= prefix.Length + suffix.Length
            || !language.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !language.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid asset language file name '{value}'.");
        }

        string stem = language[prefix.Length..^suffix.Length];
        if (stem.Length == 0
            || stem.Contains('.', StringComparison.Ordinal)
            || stem.Contains('/', StringComparison.Ordinal)
            || stem.Contains('\\', StringComparison.Ordinal)
            || !stem.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException(
                $"Invalid asset language file name '{value}'. Language stem must match [a-zA-Z0-9_] only.");
        }

        return language;
    }
}
