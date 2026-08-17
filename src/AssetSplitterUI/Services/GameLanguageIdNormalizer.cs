namespace AssetSplitterUI.Services;

/// <summary>Normalizes game language ids from UI selections, file names, and saved settings.</summary>
public static class GameLanguageIdNormalizer
{
    public static string Normalize(string? languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId))
        {
            return "";
        }

        string normalized = languageId.Trim();
        if (normalized.StartsWith("texts_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["texts_".Length..];
        }

        if (normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^".xml".Length];
        }

        normalized = normalized
            .ToLowerInvariant()
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return normalized switch
        {
            "brazilian_portuguese" => "brazilian",
            "mexican_spanish" => "mexican",
            "chinese_simplified" => "chinese",
            "simplified_chinese" => "chinese",
            "traditional_chinese" => "tchinese",
            "traditionalchinese" => "tchinese",
            _ => normalized
        };
    }

    public static string Capitalize(string languageId) =>
        languageId.Length == 0
            ? languageId
            : char.ToUpperInvariant(languageId[0]) + languageId[1..];
}
