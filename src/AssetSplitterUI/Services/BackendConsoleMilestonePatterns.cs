namespace AssetSplitterUI.Services;

/// <summary>
/// English backend stdout markers used for milestone detection and UI localization.
/// Backend console output stays English for stable pattern matching.
/// </summary>
internal static class BackendConsoleMilestonePatterns
{
    public const string ExtractionCompleteHeader = "=== EXTRACTION COMPLETE ===";
    public const string ExtractionCompleteMarker = "EXTRACTION COMPLETE";
    public const string ProcessedAssetsPrefix = "Processed ";
    public const string ProcessedAssetsSuffix = " assets.";
    public const string ProcessedResourcesSuffix = " resources.";
    public const string TotalTimePrefix = "Total time: ";
    public const string FormattingSummaryPrefix = "[INFO] Formatting summary: ";
    public const string FinalProcessingCompleted = "[COMPLETE] Final processing completed:";
    public const string AssetExtractionSuccess = "[SUCCESS] ✓ Asset extraction completed successfully!";
    public const string ExtractionCompletedSuccessfully = "[SUCCESS] Extraction completed successfully!";
    public const string RdaDecompressionSuccessful =
        "[COMPLETE] RDA decompression successful - game data now accessible as XML";
    public const string RdaDecompressionSuccessfulAvailable =
        "[COMPLETE] RDA decompression successful - game data now available as XML";
    public const string ExtractionCompleteHeaderTagged =
        "[COMPLETE] === EXTRACTION COMPLETE ===";
    public const string ProcessedZeroAssetsInfoTagged =
        "[INFO] Processed 0 assets.";
    public const string ProcessedZeroResourcesInfoTagged =
        "[INFO] Processed 0 resources.";
    public const string AssetExtractionSuccessTagged =
        "[COMPLETE] [SUCCESS] ✓ Asset extraction completed successfully!";
    public const string NoTextsEnglishInArchive =
        "[INFO] No texts_english.xml files found in the archive (not a blocker)";

    /// <summary>Backend lines that must reach the UI even when the post-run queue is dropped.</summary>
    public static bool IsImportant(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string t = text.TrimStart();

        if (t.StartsWith("=== PHASE", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.StartsWith("[PLAN", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[SUCCESS]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[COMPLETE]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[READY]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[ERROR]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[WARNING]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.StartsWith("[ISSUE_SUMMARY]", StringComparison.Ordinal)
            || t.StartsWith("[I18N] ", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[DEBUG][FLOW]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains(FinalProcessingCompleted, StringComparison.Ordinal))
        {
            return true;
        }

        if (t.StartsWith("  *", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains(ExtractionCompleteMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (t.StartsWith(ProcessedAssetsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (t.Contains(FormattingSummaryPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        if (t.StartsWith(TotalTimePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
