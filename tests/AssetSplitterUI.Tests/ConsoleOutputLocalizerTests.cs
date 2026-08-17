using AssetSplitterUI.Localization;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class ConsoleOutputLocalizerTests
{
    public ConsoleOutputLocalizerTests()
    {
        StringResourceManager.Instance.CurrentLanguage = "English";
    }

    [Fact]
    public void LocalizeProgressLine_ReplacesExtractingLabel()
    {
        string line = "[ 10.0%] [     1/    10] - Extracting: sample asset";

        string localized = ConsoleOutputLocalizer.LocalizeProgressLine(line);
        string label = StringResourceManager.Instance.GetString("consoleMessages.extractingLabel");

        Assert.Contains(label, localized);
        Assert.Contains("sample asset", localized);
    }

    [Fact]
    public void GetString_EmptyPathSegment_DoesNotThrow()
    {
        Exception? thrown = Record.Exception(() => StringResourceManager.Instance.GetString("consoleMessages."));
        Assert.Null(thrown);
    }

    [Fact]
    public async Task GetString_ConcurrentLanguageSwitch_DoesNotThrow()
    {
        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        Task[] workers = Enumerable.Range(0, 6).Select(workerIndex => Task.Run(() =>
        {
            try
            {
                for (int n = 0; n < 80; n++)
                {
                    if (n % 8 == 0)
                    {
                        StringResourceManager.Instance.CurrentLanguage = workerIndex % 2 == 0 ? "English" : "Русский";
                    }

                    _ = StringResourceManager.Instance.GetString("consoleMessages.extractingLabel");
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(20));
        StringResourceManager.Instance.CurrentLanguage = "English";
        Assert.Empty(errors);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesPhaseHeader()
    {
        string key = AssertBackendKey("=== PHASE 1: RDA EXPLORER ===", "consoleMessages.backendPhase1Label", out object[]? args);
        Assert.Null(args);
        Assert.Equal("consoleMessages.backendPhase1Label", key);
    }

    [Fact]
    public void FilterLine_HidesPlanMarkersInRegularMode()
    {
        string? filtered = ConsoleOutputLocalizer.FilterLine("[PLAN] 42", debugMode: false);

        Assert.Null(filtered);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesExtractionCompleteHeader()
    {
        string key = AssertBackendKey("=== EXTRACTION COMPLETE ===", "consoleMessages.extractionCompleteHeader", out object[]? args);
        Assert.Null(args);
        Assert.Equal("consoleMessages.extractionCompleteHeader", key);
    }

    [Fact]
    public void TryGetStructuredI18nKey_ParsesBackendMarkerWithArgs()
    {
        string line = """[I18N] {"key":"consoleMessages.processedAssetsSummary","args":["1,234"]}""";

        Assert.True(ConsoleOutputLocalizer.TryGetStructuredI18nKey(line, out string? key, out object[]? args));
        Assert.NotNull(key);
        Assert.Equal("consoleMessages.processedAssetsSummary", key);
        Assert.Equal(["1,234"], args);

        string display = ConsoleOutputLocalizer.TryResolveBackendDisplay(line);
        Assert.Equal(
            ConsoleOutputLocalizer.ResolveLocalized("consoleMessages.processedAssetsSummary", ["1,234"]),
            display);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesProcessedAssetsSummary()
    {
        string key = AssertBackendKey("Processed 1,234 assets.", "consoleMessages.processedAssetsSummary", out object[]? args);
        Assert.Equal(["1,234"], args);
        Assert.Equal("consoleMessages.processedAssetsSummary", key);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesTotalTimeSummary()
    {
        string key = AssertBackendKey("Total time: 12:34", "consoleMessages.totalTimeSummary", out object[]? args);
        Assert.Equal(["12:34"], args);
        Assert.Equal("consoleMessages.totalTimeSummary", key);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesProcessedZeroAssetsSummary()
    {
        string key = AssertBackendKey("Processed 0 assets.", "consoleMessages.processedZeroAssetsSummary", out object[]? args);
        Assert.Null(args);
        Assert.Equal("consoleMessages.processedZeroAssetsSummary", key);
    }

    [Fact]
    public void ResolveLocalized_Phase1CompleteStatus_UsesButtonLabel()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";
        string text = ConsoleOutputLocalizer.ResolveLocalized(
            "statusMessages.phase1Complete",
            ["Обработать ресурсы"]);

        Assert.Contains("Этап 1", text);
        Assert.Contains("Обработать ресурсы", text);
        Assert.DoesNotContain("Process Assets", text);
    }

    [Fact]
    public void ResolveLocalized_UsesRussianForProcessedAssetsSummary()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string localized = ConsoleOutputLocalizer.ResolveLocalized(
            "consoleMessages.processedAssetsSummary",
            ["0"]);

        Assert.Contains("ассет", localized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Processed", localized, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesRdaDecompressionSuccessful_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string key = AssertBackendKey(
            BackendConsoleMilestonePatterns.RdaDecompressionSuccessful,
            "consoleMessages.rdaDecompressionSuccessful",
            out object[]? args);
        Assert.Null(args);

        string localized = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Assert.Contains("Распаковка RDA", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("decompression successful", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = "Wave-1 i18n gap: successExtractionComplete is still English in Strings.ru.json.")]
    public void TryGetBackendConsoleLocalizationKey_MatchesAssetExtractionSuccess_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string key = AssertBackendKey(
            BackendConsoleMilestonePatterns.AssetExtractionSuccess,
            "consoleMessages.successExtractionComplete",
            out object[]? args);

        string localized = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Assert.Contains("УСПЕХ", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("Asset extraction completed", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesExtractionCompletedSuccessfullyLine_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string key = AssertBackendKey(
            BackendConsoleMilestonePatterns.ExtractionCompletedSuccessfully,
            "consoleMessages.extractionCompletedSuccessfully",
            out object[]? args);

        string localized = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Assert.Contains("Извлечение завершено успешно", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("Extraction completed successfully", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesNoTextsEnglishInArchive_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string key = AssertBackendKey(
            BackendConsoleMilestonePatterns.NoTextsEnglishInArchive,
            "consoleMessages.noTextsEnglishInArchive",
            out object[]? args);

        string localized = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Assert.Contains("texts_english.xml", localized, StringComparison.Ordinal);
        Assert.Contains("не критично", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("not a blocker", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetBackendConsoleLocalizationKey_MatchesExtractionCompleteHeader_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string key = AssertBackendKey(
            BackendConsoleMilestonePatterns.ExtractionCompleteHeader,
            "consoleMessages.extractionCompleteHeader",
            out object[]? args);

        string localized = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Assert.Contains("ИЗВЛЕЧЕНИЕ ЗАВЕРШЕНО", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("EXTRACTION COMPLETE", localized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[INFO] Processed 0 assets.", "consoleMessages.processedZeroAssetsSummary")]
    [InlineData("[INFO] Processed 0 resources.", "consoleMessages.processedZeroAssetsSummary")]
    [InlineData("[COMPLETE] === EXTRACTION COMPLETE ===", "consoleMessages.extractionCompleteHeader")]
    [InlineData("[INFO] Total time: 00:00", "consoleMessages.totalTimeSummary")]
    [InlineData(BackendConsoleMilestonePatterns.RdaDecompressionSuccessfulAvailable, "consoleMessages.rdaDecompressionSuccessful")]
    [InlineData(BackendConsoleMilestonePatterns.AssetExtractionSuccessTagged, "consoleMessages.successExtractionComplete")]
    [InlineData(BackendConsoleMilestonePatterns.ExtractionCompletedSuccessfully, "consoleMessages.extractionCompletedSuccessfully")]
    public void TryGetBackendConsoleLocalizationKey_MatchesDebugModeTaggedVariants(string backendLine, string expectedKey)
    {
        _ = AssertBackendKey(backendLine, expectedKey, out _);
    }

    [Fact(Skip = "Wave-1 i18n gap: processed-assets debug summary is still English in Russian.")]
    public void TryResolveBackendDisplay_LocalizesDebugModePhase1Summary_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string localized = ConsoleOutputLocalizer.TryResolveBackendDisplay("[INFO] Processed 0 assets.");

        Assert.Contains("этап 1", localized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Processed 0 assets", localized, StringComparison.Ordinal);
    }

    [Fact(Skip = "Debug-mode RDA complete line is not in the Russian localizer table yet.")]
    public void TryResolveBackendDisplay_LocalizesDebugModeRdaComplete_Russian()
    {
        StringResourceManager.Instance.CurrentLanguage = "Русский";

        string localized = ConsoleOutputLocalizer.TryResolveBackendDisplay(
            BackendConsoleMilestonePatterns.RdaDecompressionSuccessfulAvailable);

        Assert.Contains("Распаковка RDA", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("decompression successful", localized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterLine_AllowsPlanMarkersInDebugMode()
    {
        string? filtered = ConsoleOutputLocalizer.FilterLine("[PLAN] 42", debugMode: true);

        Assert.Equal("[PLAN] 42", filtered);
    }

    [Fact]
    public void TryResolveBackendDisplay_PrefersStructuredI18nOverEnglishMatch()
    {
        string i18n = """[I18N] {"key":"consoleMessages.pipelineStarting"}""";
        string display = ConsoleOutputLocalizer.TryResolveBackendDisplay(i18n);
        Assert.Equal(
            ConsoleOutputLocalizer.ResolveLocalized("consoleMessages.pipelineStarting", null),
            display);
    }

    /// <summary>
    /// Asserts a successful backend-key match and returns a non-null key.
    /// <see cref="Assert.NotNull"/> documents the Try* contract for the compiler and for readers.
    /// </summary>
    private static string AssertBackendKey(string backendLine, string expectedKey, out object[]? args)
    {
        Assert.True(
            ConsoleOutputLocalizer.TryGetBackendConsoleLocalizationKey(backendLine, out string? key, out args),
            $"Expected localization match for: {backendLine}");
        Assert.NotNull(key);
        Assert.Equal(expectedKey, key);
        return key;
    }
}
