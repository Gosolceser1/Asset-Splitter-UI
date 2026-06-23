namespace AssetSplitterUI.Localization;

/// <summary>Resolves issue summary text from UI strings by issue code (not from English JSON report text).</summary>
internal static class IssueSummaryLocalizer
{
    public static string GetGroupTitle(string code)
    {
        string key = $"issueSummary.codes.{code}.title";
        string text = StringResourceManager.Instance.GetString(key);
        return text == key ? code : text;
    }

    public static string GetRootCause(string code, string? jsonFallback = null)
    {
        string key = $"issueSummary.codes.{code}.rootCause";
        string text = StringResourceManager.Instance.GetString(key);
        return text == key ? (jsonFallback ?? "") : text;
    }

    public static string GetHint(string code, string? jsonFallback = null)
    {
        string key = $"issueSummary.codes.{code}.hint";
        string text = StringResourceManager.Instance.GetString(key);
        if (text == key)
        {
            return jsonFallback ?? "";
        }

        return text;
    }

    public static string SampleBulletPrefix =>
        StringResourceManager.Instance.GetString("issueSummary.sampleBullet");

    public static string SampleFileDetail(string fileName, string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? fileName
            : string.Format(
                StringResourceManager.Instance.GetString("issueSummary.sampleFileDetail"),
                fileName,
                detail);
}
