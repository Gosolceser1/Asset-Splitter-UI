namespace AssetProcessor;

public static class TranslationRegistry
{
    public static string Translate(PipelineContext context, string guid)
    {
        if (context.Translator.TryGetValue(guid, out string? translatorValue) && translatorValue is not null)
            return translatorValue;
        if (context.AssetNames.TryGetValue(guid, out string? assetValue) && assetValue is not null)
            return assetValue;
        return string.Empty;
    }
}
