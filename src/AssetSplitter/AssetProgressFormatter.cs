namespace AssetProcessor;

/// <summary>
/// Structured progress labels for the GUI console in regular (non-debug) mode.
/// Matches phase-3 extraction shape: <c>Verb: {guid} - {name} ({template})</c>.
/// Not used when <see cref="PipelineContext.DebugMode"/> is on.
/// </summary>
internal static class AssetProgressFormatter
{
    public static string FromAssetFileStem(string verbWithColon, string fileNameWithoutExtension, string? templateName = null)
    {
        TryParseAssetFileStem(fileNameWithoutExtension, out string guid, out string displayName);
        return Format(verbWithColon, guid, displayName, templateName);
    }

    public static string Format(string verbWithColon, string guid, string displayName, string? templateName = null)
    {
        string verb = NormalizeVerb(verbWithColon);
        string safeName = string.IsNullOrWhiteSpace(displayName) ? guid : displayName.Trim();
        if (!string.IsNullOrEmpty(guid) && safeName.StartsWith(guid, StringComparison.Ordinal))
        {
            safeName = safeName[guid.Length..].TrimStart(' ', '-');
        }

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = guid;
        }

        if (!string.IsNullOrEmpty(guid) && guid.All(char.IsDigit))
        {
            if (!string.IsNullOrWhiteSpace(templateName))
            {
                return $"{verb} {guid} - {safeName} ({templateName.Trim()})";
            }

            return $"{verb} {guid} - {safeName}";
        }

        return $"{verb} {safeName}";
    }

    public static bool TryParseAssetFileStem(string fileNameWithoutExtension, out string guid, out string displayName)
    {
        guid = "";
        displayName = AssetProcessorFileSystem.ExtractDisplayName(fileNameWithoutExtension);

        int sep = fileNameWithoutExtension.IndexOf(" - [ ", StringComparison.Ordinal);
        if (sep <= 0)
        {
            return false;
        }

        string candidate = fileNameWithoutExtension[..sep].Trim();
        if (!candidate.All(char.IsDigit))
        {
            return false;
        }

        guid = candidate;
        return true;
    }

    private static string NormalizeVerb(string verbWithColon)
    {
        string verb = verbWithColon.Trim();
        return verb.EndsWith(":", StringComparison.Ordinal) ? verb : verb + ":";
    }
}
