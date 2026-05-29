using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

/// <summary>Semantic category of a console log line, used to choose its color.</summary>
public enum LogLineKind
{
    Normal,
    Progress,
    Processing,
    Command,
    Header,
    Separator,
    Phase,
    Subsystem,
    Summary,
    Trace,
    OptionOn,
    OptionOff,
    Info,
    Success,
    Error,
    Warning,
    Debug,
    Empty,
}

/// <summary>One console log entry: text, display category, and optional localization metadata for language refresh.</summary>
public sealed class LogLine
{
    public string Text { get; }
    public LogLineKind Kind { get; }
    public int RepeatCount { get; set; } = 1;
    public string? LocalizationKey { get; }
    public object[]? LocalizationArgs { get; }
    public string? OriginalText { get; }

    public string DisplayText => RepeatCount <= 1 ? Text : $"{Text} ×{RepeatCount}";
    public bool IsStructuredProgress =>
        ConsoleProgressLineParser.TrySplit(Text, out _, out _, out _, out _, out _, out _)
        || (OriginalText is not null
            && OriginalText != Text
            && ConsoleProgressLineParser.TrySplit(OriginalText, out _, out _, out _, out _, out _, out _));
    public string ProgressMetrics =>
        TrySplitProgress(out var metrics, out _, out _, out _, out _, out _) ? metrics : "";
    public string ProgressOperation =>
        TrySplitProgress(out _, out var operation, out _, out _, out _, out _) ? operation : "";
    public string ProgressAssetDetail =>
        TrySplitProgress(out _, out _, out var detail, out _, out _, out _) ? detail : "";
    public string ProgressGuid =>
        TrySplitProgress(out _, out _, out _, out _, out var guid, out _) ? guid : "";
    public string ProgressGuidSeparator => HasProgressGuid && HasProgressAssetName ? " - " : "";
    public string ProgressAssetName =>
        TrySplitProgress(out _, out _, out _, out _, out _, out var name) ? name : "";
    public string ProgressTemplateDetail =>
        TrySplitProgress(out _, out _, out _, out var template, out _, out _) ? template : "";

    private bool TrySplitProgress(
        out string metrics,
        out string operation,
        out string assetDetail,
        out string templateDetail,
        out string guid,
        out string assetName)
    {
        if (ConsoleProgressLineParser.TrySplit(Text, out metrics, out operation, out assetDetail, out templateDetail, out guid, out assetName))
            return true;

        metrics = operation = assetDetail = templateDetail = guid = assetName = "";
        return OriginalText is not null
            && ConsoleProgressLineParser.TrySplit(OriginalText, out metrics, out operation, out assetDetail, out templateDetail, out guid, out assetName);
    }
    public string ProgressAssetSeparator => HasProgressAssetDetail ? " " : "";
    public bool HasProgressAssetDetail => ProgressAssetDetail.Length > 0;
    public bool HasProgressGuid => ProgressGuid.Length > 0;
    public bool HasProgressAssetName => ProgressAssetName.Length > 0;
    public bool HasProgressTemplateDetail => ProgressTemplateDetail.Length > 0;

    private LogLine(string text, LogLineKind kind, string? localizationKey, object[]? localizationArgs, string? originalText)
    {
        Text = text;
        Kind = kind;
        LocalizationKey = localizationKey;
        LocalizationArgs = localizationArgs;
        OriginalText = originalText;
    }

    public static LogLine From(string text) => new(text, ConsoleLineClassifier.Classify(text), null, null, null);

    public static LogLine FromLocalized(string text, string key, object[]? args) =>
        new(text, ConsoleLineClassifier.Classify(text), key, args, null);

    public static LogLine FromBackendOutput(string originalText, string displayText) =>
        new(displayText, ConsoleLineClassifier.Classify(displayText), null, null, originalText);

    public LogLine WithText(string newText) =>
        new(newText, ConsoleLineClassifier.Classify(newText), LocalizationKey, LocalizationArgs, OriginalText);
}
