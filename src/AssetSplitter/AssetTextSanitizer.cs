namespace AssetProcessor;

internal static class AssetTextSanitizer
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static string FormatAssetFileName(string guid, string name, string template, string translatedName)
    {
        string safeGuid = SanitizeGuidForFileName(guid);

        string selectedName = !string.IsNullOrWhiteSpace(translatedName) ? translatedName : name;
        if (string.IsNullOrWhiteSpace(selectedName) && !string.IsNullOrWhiteSpace(template))
        {
            selectedName = template;
        }

        if (string.IsNullOrWhiteSpace(selectedName))
        {
            selectedName = safeGuid;
        }

        string safeName = SanitizeFileNamePart(selectedName, maxLength: 64);
        return $"{safeGuid} - [ {safeName} ].xml";
    }

    /// <summary>
    /// Prefers digits-only GUIDs; otherwise sanitizes filename chars and never allows <c>\</c>, <c>/</c>, or <c>..</c>.
    /// </summary>
    private static string SanitizeGuidForFileName(string guid)
    {
        string trimmed = (guid ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "unknown";
        }

        string safeGuid = SanitizeFileNamePart(trimmed, maxLength: 64);
        safeGuid = safeGuid.Replace('\\', '_').Replace('/', '_');
        while (safeGuid.Contains("..", StringComparison.Ordinal))
        {
            safeGuid = safeGuid.Replace("..", "_", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(safeGuid) || safeGuid is "." or "..")
        {
            return "unknown";
        }

        return safeGuid;
    }

    public static string SanitizeFileNamePart(string value, int? maxLength = null)
    {
        string safeValue = maxLength is null ? value : value[..Math.Min(value.Length, maxLength.Value)];
        safeValue = safeValue.Replace("\n", "").Replace("\r", "").Replace("\t", "").Trim();
        while (safeValue.Contains("  ", StringComparison.Ordinal))
        {
            safeValue = safeValue.Replace("  ", " ", StringComparison.Ordinal);
        }

        foreach (char invalidFileNameChar in InvalidFileNameChars)
        {
            safeValue = safeValue.Replace(invalidFileNameChar, '_');
        }

        return safeValue is "." or ".." ? "_" : safeValue;
    }

    /// <summary>
    /// Makes <paramref name="text"/> valid XML comment content: no "--" anywhere (a single
    /// Replace turned "---" into "--") and no trailing "-" (which would form "-->" early).
    /// </summary>
    public static string ToSafeXmlCommentBody(string text)
    {
        string value = text;
        while (value.Contains("--", StringComparison.Ordinal))
        {
            value = value.Replace("--", "-", StringComparison.Ordinal);
        }

        value = value.Trim();
        return value.EndsWith('-') ? value + " " : value;
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
        {
            sanitizedValue = sanitizedValue.Replace("--", "-", StringComparison.Ordinal);
        }

        string trimmedValue = sanitizedValue.Trim('-', '_', ' ').Trim();
        if (string.IsNullOrWhiteSpace(trimmedValue))
        {
            return null;
        }

        return trimmedValue.Length <= 128 ? $" {trimmedValue} " : $" {trimmedValue[..128]}... ";
    }
}
