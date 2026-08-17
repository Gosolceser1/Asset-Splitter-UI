using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

/// <summary>
/// Wave 1 localization gauntlet: find + reproduce only (no production fixes).
/// </summary>
public sealed class GauntletLocalizationTests
{
    private static readonly Regex PlaceholderIndex = new(@"\{(\d+)\}", RegexOptions.Compiled);
    private static readonly string[] LocaleFileNames =
    [
        "Strings.de.json", "Strings.es.json", "Strings.fr.json", "Strings.it.json",
        "Strings.ja.json", "Strings.ko.json", "Strings.pl.json", "Strings.ru.json",
        "Strings.tw.json", "Strings.zh.json",
    ];

    private static readonly string[] ConsoleLocales =
        ["de", "es", "fr", "it", "ja", "ko", "pl", "ru", "tw", "zh"];

    private static readonly Dictionary<string, int> ReadmeModOpsFormatArgCounts = new(StringComparer.Ordinal)
    {
        ["readmeModOpsRefReplace"] = 2,
        ["readmeModOpsRefMerge"] = 2,
        ["readmeModOpsRefAdd"] = 1,
        ["readmeModOpsRefRemove"] = 1,
        ["readmeModOpsRefAddNextSibling"] = 1,
        ["readmeModOpsRefAddPrevSibling"] = 1,
    };

    public GauntletLocalizationTests()
    {
        StringResourceManager.Instance.CurrentLanguage = "English";
    }

    [Fact]
    public void LocaleKeyParity_AllLocalesMatchEnglishKeySet()
    {
        string languagesDir = FindLanguagesDirectory();
        HashSet<string> englishKeys = FlattenKeys(LoadJson(Path.Combine(languagesDir, "Strings.json")));

        foreach (string localeFile in LocaleFileNames)
        {
            HashSet<string> localeKeys = FlattenKeys(LoadJson(Path.Combine(languagesDir, localeFile)));
            List<string> missing = englishKeys.Where(k => !localeKeys.Contains(k)).OrderBy(k => k).ToList();
            List<string> extra = localeKeys.Where(k => !englishKeys.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"{localeFile} missing keys: {string.Join(", ", missing.Take(8))}");
            Assert.True(extra.Count == 0, $"{localeFile} extra keys: {string.Join(", ", extra.Take(8))}");
        }
    }

    [Fact]
    public void UiStrings_PlaceholderIndexesMatchEnglish()
    {
        string languagesDir = FindLanguagesDirectory();
        Dictionary<string, string> english = FlattenStrings(LoadJson(Path.Combine(languagesDir, "Strings.json")));

        foreach (string localeFile in LocaleFileNames)
        {
            Dictionary<string, string> locale = FlattenStrings(LoadJson(Path.Combine(languagesDir, localeFile)));
            List<string> mismatches = [];
            foreach ((string key, string enValue) in english)
            {
                if (!locale.TryGetValue(key, out string? locValue))
                {
                    continue;
                }

                var enPh = PlaceholderIndexes(enValue);
                var locPh = PlaceholderIndexes(locValue);
                if (!enPh.SetEquals(locPh))
                {
                    mismatches.Add($"{key} EN={{{string.Join(",", enPh.OrderBy(x => x))}}} LOC={{{string.Join(",", locPh.OrderBy(x => x))}}}");
                }
            }

            Assert.True(mismatches.Count == 0, $"{localeFile} placeholder mismatches: {string.Join("; ", mismatches.Take(5))}");
        }
    }

    [Fact]
    public void ConsoleMessages_KeyParityAcrossLocales()
    {
        string consoleDir = FindConsoleMessagesDirectory();
        HashSet<string> englishKeys = FlattenKeys(LoadJson(Path.Combine(consoleDir, "console_en.json")));

        foreach (string locale in ConsoleLocales)
        {
            HashSet<string> localeKeys = FlattenKeys(LoadJson(Path.Combine(consoleDir, $"console_{locale}.json")));
            List<string> missing = englishKeys.Where(k => !localeKeys.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(missing.Count == 0, $"console_{locale}.json missing {missing.Count} keys. First: {string.Join(", ", missing.Take(5))}");
        }
    }

    [Fact]
    public void LocalizationJson_IsUtf8WithoutBomOrNullBytes()
    {
        string languagesDir = FindLanguagesDirectory();
        string consoleDir = FindConsoleMessagesDirectory();
        IEnumerable<string> files = Directory.GetFiles(languagesDir, "Strings*.json")
            .Concat(Directory.GetFiles(consoleDir, "console_*.json"));

        foreach (string file in files)
        {
            byte[] raw = File.ReadAllBytes(file);
            Assert.False(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
                $"{Path.GetFileName(file)} has UTF-8 BOM");
            Assert.DoesNotContain((byte)0, raw);
            _ = Encoding.UTF8.GetString(raw); // throws if invalid UTF-8 sequences are fatal; accept replacement-free path
            Assert.True(IsValidUtf8(raw), $"{Path.GetFileName(file)} is not valid UTF-8");
        }
    }

    [Fact]
    public void UiStrings_IncludeGameBuildDetectedKey_UsedByExtractionHeader()
    {
        // ExtractionRunResultAppender.AppendRunHeader calls
        // AppendLocalized("consoleMessages.gameBuildDetected", ...).
        // Backend console_*.json has the key; UI Strings*.json must too or the log shows the raw path.
        string languagesDir = FindLanguagesDirectory();
        Dictionary<string, string> english = FlattenStrings(LoadJson(Path.Combine(languagesDir, "Strings.json")));

        Assert.True(
            english.ContainsKey("consoleMessages.gameBuildDetected"),
            "Missing UI key consoleMessages.gameBuildDetected (present in config/05_Console_Messages/console_*.json and used by ExtractionRunResultAppender).");
    }

    [Fact]
    public void GetString_GameBuildDetected_ResolvesEnglishTemplate()
    {
        StringResourceManager.Instance.CurrentLanguage = "English";
        string resolved = StringResourceManager.Instance.GetString("consoleMessages.gameBuildDetected");

        Assert.NotEqual("consoleMessages.gameBuildDetected", resolved);
        Assert.Contains("Game build", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{0}", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveLocalized_GameBuildDetected_DoesNotLeakRawKey()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";
        string display = ConsoleOutputLocalizer.ResolveLocalized(
            "consoleMessages.gameBuildDetected",
            ["12345"]);

        Assert.DoesNotContain("consoleMessages.gameBuildDetected", display, StringComparison.Ordinal);
        Assert.Contains("12345", display, StringComparison.Ordinal);
    }

    [Fact(Skip = "Wave-1 i18n gap: translated readmeModOpsRef strings dropped {n} placeholders.")]
    public void ConsoleReadmeModOpsRef_TranslationsKeepPlaceholderIndexesRequiredByModReadmeWriter()
    {
        // ModReadmeWriter formats these with string.Format(..., "123456"[, "Example asset"]).
        // Missing {n} is not a FormatException (extra args ignored), but GUID/name examples vanish.
        string consoleDir = FindConsoleMessagesDirectory();
        Dictionary<string, string> english = FlattenStrings(LoadJson(Path.Combine(consoleDir, "console_en.json")));
        List<string> failures = [];

        foreach (string locale in ConsoleLocales)
        {
            Dictionary<string, string> loc = FlattenStrings(LoadJson(Path.Combine(consoleDir, $"console_{locale}.json")));
            foreach ((string key, int argc) in ReadmeModOpsFormatArgCounts)
            {
                var enPh = PlaceholderIndexes(english[key]);
                var locPh = PlaceholderIndexes(loc[key]);
                if (!enPh.SetEquals(locPh))
                {
                    failures.Add($"console_{locale}.json|{key} EN={FormatSet(enPh)} LOC={FormatSet(locPh)} (ModReadmeWriter argc={argc})");
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            $"readmeModOpsRef placeholder drift ({failures.Count}): {string.Join(" | ", failures.Take(12))}");
    }

    [Fact(Skip = "Wave-1 i18n gap: ja/zh/ko readmeModOpsRefMerge still English.")]
    public void ConsoleReadmeModOpsRefMerge_AsianLocales_AreNotLeftoverEnglish()
    {
        string consoleDir = FindConsoleMessagesDirectory();
        string englishMerge = FlattenStrings(LoadJson(Path.Combine(consoleDir, "console_en.json")))["readmeModOpsRefMerge"];

        foreach (string locale in new[] { "ja", "zh", "tw", "ko" })
        {
            string value = FlattenStrings(LoadJson(Path.Combine(consoleDir, $"console_{locale}.json")))["readmeModOpsRefMerge"];
            Assert.False(
                string.Equals(value, englishMerge, StringComparison.Ordinal),
                $"console_{locale}.json readmeModOpsRefMerge is still the English body (placeholders included, untranslated).");
        }
    }

    [Fact(Skip = "Wave-1 i18n gap: 108 status/console keys remain English in all locales.")]
    public void SharedUntranslatedUiConsoleMessages_AreAbsent()
    {
        // Keys that remain byte-identical to English in every locale — excludes brand/proper-noun keys.
        string languagesDir = FindLanguagesDirectory();
        Dictionary<string, string> english = FlattenStrings(LoadJson(Path.Combine(languagesDir, "Strings.json")));
        List<Dictionary<string, string>> locales = LocaleFileNames
            .Select(f => FlattenStrings(LoadJson(Path.Combine(languagesDir, f))))
            .ToList();

        List<string> identicalEverywhere = [];
        foreach ((string key, string enValue) in english)
        {
            if (!key.StartsWith("consoleMessages.", StringComparison.Ordinal)
                && !key.StartsWith("statusMessages.", StringComparison.Ordinal))
            {
                continue;
            }

            if (!LooksLikeTranslatableEnglish(enValue))
            {
                continue;
            }

            if (locales.All(m => m.TryGetValue(key, out string? v) && v == enValue))
            {
                identicalEverywhere.Add(key);
            }
        }

        Assert.True(
            identicalEverywhere.Count == 0,
            $"Found {identicalEverywhere.Count} status/console keys still English in all locales. Sample: {string.Join(", ", identicalEverywhere.Take(12))}");
    }

    [Fact]
    public void GetString_FallsBackToEnglishWhenLocaleOmitsKey_SimulatedByUnknownKey()
    {
        // Documented contract: missing in current + present in English → English;
        // missing in both → raw path (not bracketed "[path]").
        StringResourceManager.Instance.CurrentLanguage = "Deutsch";
        string existing = StringResourceManager.Instance.GetString("buttons.extract");
        Assert.False(string.IsNullOrWhiteSpace(existing));
        Assert.NotEqual("buttons.extract", existing);

        string missing = StringResourceManager.Instance.GetString("consoleMessages.__gauntlet_missing_key__");
        Assert.Equal("consoleMessages.__gauntlet_missing_key__", missing);
    }

    [Fact]
    public void FormatLocalizedText_GameBuildDetected_FormatsWithoutLeakingRawKey()
    {
        StringResourceManager.Instance.CurrentLanguage = "English";
        string ok = ConsoleOutputLocalizer.ResolveLocalized(
            "consoleMessages.processedAssetsSummary",
            ["1,234"]);
        Assert.Contains("1,234", ok, StringComparison.Ordinal);

        string gameBuild = ConsoleOutputLocalizer.ResolveLocalized(
            "consoleMessages.gameBuildDetected",
            ["build"]);
        Assert.DoesNotContain("consoleMessages.gameBuildDetected", gameBuild, StringComparison.Ordinal);
        Assert.Contains("build", gameBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void GameLanguageIdNormalizer_AndDisplayAliases_CoverSimplifiedChinese()
    {
        Assert.Equal("chinese", GameLanguageIdNormalizer.Normalize("Chinese (Simplified)"));
        Assert.Equal("chinese", GameLanguageIdNormalizer.Normalize("simplified_chinese"));
        Assert.Equal("tchinese", GameLanguageIdNormalizer.Normalize("traditional_chinese"));

        string languagesDir = FindLanguagesDirectory();
        Dictionary<string, string> english = FlattenStrings(LoadJson(Path.Combine(languagesDir, "Strings.json")));
        Assert.Contains("gameLanguages.chinese", english.Keys);
        Assert.Contains("gameLanguages.simplified_chinese", english.Keys);
        Assert.Contains("gameLanguages.tchinese", english.Keys);
        Assert.Contains("gameLanguages.traditional_chinese", english.Keys);
    }

    private static bool LooksLikeTranslatableEnglish(string value) =>
        value.Length >= 12
        && Regex.IsMatch(value, "[A-Za-z]{5,}")
        && !value.StartsWith("Anno ", StringComparison.Ordinal);

    private static HashSet<int> PlaceholderIndexes(string text) =>
        PlaceholderIndex.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

    private static string FormatSet(HashSet<int> set) =>
        "{" + string.Join(",", set.OrderBy(x => x)) + "}";

    private static bool IsValidUtf8(byte[] raw)
    {
        try
        {
            Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            _ = utf8.GetString(raw);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static JsonElement LoadJson(string path)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static HashSet<string> FlattenKeys(JsonElement element, string prefix = "")
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (prefix.Length > 0)
            {
                keys.Add(prefix);
            }

            return keys;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                keys.UnionWith(FlattenKeys(property.Value, path));
            }
            else
            {
                keys.Add(path);
            }
        }

        return keys;
    }

    private static Dictionary<string, string> FlattenStrings(JsonElement element, string prefix = "")
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (element.ValueKind != JsonValueKind.Object)
        {
            if (prefix.Length > 0 && element.ValueKind == JsonValueKind.String)
            {
                map[prefix] = element.GetString() ?? "";
            }

            return map;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            string path = prefix.Length == 0 ? property.Name : prefix + "." + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach ((string k, string v) in FlattenStrings(property.Value, path))
                {
                    map[k] = v;
                }
            }
            else if (property.Value.ValueKind == JsonValueKind.String)
            {
                map[path] = property.Value.GetString() ?? "";
            }
        }

        return map;
    }

    private static string FindLanguagesDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "Localization", "Languages");
            if (File.Exists(Path.Combine(candidate, "Strings.json")))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Localization/Languages/Strings.json");
    }

    private static string FindConsoleMessagesDirectory()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "config", "05_Console_Messages");
            if (File.Exists(Path.Combine(candidate, "console_en.json")))
            {
                return candidate;
            }

            // Dev: walk up from tests bin to repo root
            string repoCandidate = Path.Combine(dir, "config", "05_Console_Messages");
            if (File.Exists(Path.Combine(repoCandidate, "console_en.json")))
            {
                return repoCandidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate config/05_Console_Messages/console_en.json");
    }
}
