using AssetSplitterUI.Localization;
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
    private readonly Lazy<ProgressParts?> _progressParts;

    public string Text { get; }
    public LogLineKind Kind { get; }
    public int RepeatCount { get; set; } = 1;
    public string? LocalizationKey { get; }
    public object[]? LocalizationArgs { get; }
    public string? OriginalText { get; }

    public string DisplayText => RepeatCount <= 1 ? Text : $"{Text} ×{RepeatCount}";
    public bool IsStructuredProgress => _progressParts.Value is not null;
    public string ProgressMetrics => _progressParts.Value?.Metrics ?? "";
    public string ProgressOperation => _progressParts.Value?.Operation ?? "";
    public string ProgressAssetDetail => _progressParts.Value?.AssetDetail ?? "";
    public string ProgressGuid => _progressParts.Value?.Guid ?? "";
    public string ProgressGuidSeparator => HasProgressGuid && HasProgressAssetName
        ? StringResourceManager.Instance.GetString("consoleMessages.progressSeparator")
        : "";
    public string ProgressOperationSeparator => StringResourceManager.Instance.GetString("consoleMessages.progressSeparator");
    public string ProgressAssetName => _progressParts.Value?.AssetName ?? "";
    public string ProgressTemplateDetail => _progressParts.Value?.TemplateDetail ?? "";
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
        _progressParts = new Lazy<ProgressParts?>(ParseProgressParts);
    }

    public static LogLine From(string text) => new(text, ConsoleLineClassifier.Classify(text), null, null, null);

    public static LogLine FromLocalized(string text, string key, object[]? args) =>
        new(text, ConsoleLineClassifier.Classify(text), key, args, null);

    public static LogLine FromBackendOutput(string originalText, string displayText) =>
        new(displayText, ConsoleLineClassifier.Classify(displayText), null, null, originalText);

    public LogLine WithText(string newText) =>
        new(newText, ConsoleLineClassifier.Classify(newText), LocalizationKey, LocalizationArgs, OriginalText);

    private ProgressParts? ParseProgressParts()
    {
        if (ConsoleProgressLineParser.TrySplit(
                Text,
                out string metrics,
                out string operation,
                out string assetDetail,
                out string templateDetail,
                out string guid,
                out string assetName))
        {
            return new ProgressParts(metrics, operation, assetDetail, templateDetail, guid, assetName);
        }

        if (OriginalText is not null
            && ConsoleProgressLineParser.TrySplit(
                OriginalText,
                out metrics,
                out operation,
                out assetDetail,
                out templateDetail,
                out guid,
                out assetName))
        {
            return new ProgressParts(metrics, operation, assetDetail, templateDetail, guid, assetName);
        }

        return null;
    }

    private sealed record ProgressParts(
        string Metrics,
        string Operation,
        string AssetDetail,
        string TemplateDetail,
        string Guid,
        string AssetName);
}
