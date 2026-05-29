namespace AssetProcessor;

internal static class AssetTextSanitizer
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string FormatAssetFileName(string guid, string name, string template, string translatedName)
    {
        string selectedName = !string.IsNullOrWhiteSpace(translatedName) ? translatedName : name;
        if (string.IsNullOrWhiteSpace(selectedName) && !string.IsNullOrWhiteSpace(template))
        {
            selectedName = template;
        }

        if (string.IsNullOrWhiteSpace(selectedName))
        {
            selectedName = guid;
        }

        string safeName = SanitizeFileNamePart(selectedName, maxLength: 64);
        return $"{guid} - [ {safeName} ].xml";
    }

    public static string SanitizeFileNamePart(string value, int? maxLength = null)
    {
        string safeValue = maxLength is null ? value : value[..Math.Min(value.Length, maxLength.Value)];
        safeValue = safeValue.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();
        while (safeValue.Contains("  ", StringComparison.Ordinal))
            safeValue = safeValue.Replace("  ", " ", StringComparison.Ordinal);

        foreach (char invalidFileNameChar in InvalidFileNameChars)
        {
            safeValue = safeValue.Replace(invalidFileNameChar, '_');
        }

        return safeValue;
    }

    public static string? ToXmlCommentText(string translatedValue)
    {
        StringBuilder builder = new(translatedValue, translatedValue.Length + 10);
        foreach (char invalidChar in InvalidFileNameChars)
        {
            builder.Replace(invalidChar, '_');
        }

        builder.Replace("<", "").Replace(">", "").Replace("!", "");

        string sanitizedValue = builder.ToString();
        while (sanitizedValue.Contains("--", StringComparison.Ordinal))
            sanitizedValue = sanitizedValue.Replace("--", "-", StringComparison.Ordinal);

        string trimmedValue = sanitizedValue.Trim('-', '_', ' ').Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            return null;
        }

        return trimmedValue.Length <= 128 ? $" {trimmedValue} " : $" {trimmedValue[..128]}... ";
    }
}
