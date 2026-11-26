using System.Diagnostics.CodeAnalysis;

namespace AssetProcessor;

/// <summary>Maps language folder / CLI language tokens to Anno mod language keys.</summary>
internal static class GameLanguageKeys
{
    private static readonly Dictionary<string, string> ToAnnoKey = new(StringComparer.OrdinalIgnoreCase)
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
        ["taiwanese"] = "Chinese (Traditional)",
        ["czech"] = "Czech",
        ["portuguese"] = "Portuguese",
        ["brazilian"] = "Brazilian Portuguese",
        ["mexican"] = "Mexican Spanish",
        ["simplified_chinese"] = "Chinese (Simplified)",
        ["traditional_chinese"] = "Chinese (Traditional)",
    };

    public static string GetAnnoKeyOrEnglish(string assetLanguage) =>
        TryGetAnnoKey(assetLanguage, out string? key) ? key : "English";

    public static bool TryGetAnnoKey(string assetLanguage, [NotNullWhen(true)] out string? key) =>
        ToAnnoKey.TryGetValue(assetLanguage, out key);
}
