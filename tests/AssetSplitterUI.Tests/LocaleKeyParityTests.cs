using System.Text.Json;

namespace AssetSplitterUI.Tests;

public sealed class LocaleKeyParityTests
{
    [Fact]
    public void AllLocaleFiles_ContainEveryEnglishKey()
    {
        string languagesDir = FindLanguagesDirectory();
        using JsonDocument english = JsonDocument.Parse(File.ReadAllText(Path.Combine(languagesDir, "Strings.json")));
        HashSet<string> englishKeys = Flatten(english.RootElement);

        string[] localeFiles = Directory.GetFiles(languagesDir, "Strings.*.json");
        Assert.NotEmpty(localeFiles);

        foreach (string localeFile in localeFiles)
        {
            using JsonDocument locale = JsonDocument.Parse(File.ReadAllText(localeFile));
            HashSet<string> localeKeys = Flatten(locale.RootElement);
            List<string> missing = englishKeys.Where(k => !localeKeys.Contains(k)).OrderBy(k => k).ToList();
            Assert.True(
                missing.Count == 0,
                $"{Path.GetFileName(localeFile)} missing {missing.Count} keys. First: {string.Join(", ", missing.Take(5))}");
        }
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

            string repoCandidate = Path.Combine(dir, "Languages");
            if (File.Exists(Path.Combine(repoCandidate, "Strings.json")))
            {
                return repoCandidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        // Test project copies Localization to output as Localization\...
        string outputCopy = Path.Combine(AppContext.BaseDirectory, "Localization", "Languages");
        if (File.Exists(Path.Combine(outputCopy, "Strings.json")))
        {
            return outputCopy;
        }

        throw new DirectoryNotFoundException("Could not locate Localization/Languages/Strings.json");
    }

    private static HashSet<string> Flatten(JsonElement element, string prefix = "")
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
                keys.UnionWith(Flatten(property.Value, path));
            }
            else
            {
                keys.Add(path);
            }
        }

        return keys;
    }
}
