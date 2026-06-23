namespace AssetSplitterUI.Services;

/// <summary>
/// Builds human-readable run status (phase title + current asset) for the status header.
/// </summary>
internal sealed class ConsoleRunStatusPresenter
{
    public string PhaseTitle { get; private set; } = "";
    public string OperationCounts { get; private set; } = "";
    public string StepPercent { get; private set; } = "";
    public string RawProgressLine { get; private set; } = "";
    public string Operation { get; private set; } = "";
    public string Guid { get; private set; } = "";
    public string AssetName { get; private set; } = "";
    public string TemplateDetail { get; private set; } = "";
    public string GuidSeparator { get; private set; } = "";
    public bool HasLiveDetail { get; private set; }

    public void Reset()
    {
        PhaseTitle = "";
        OperationCounts = "";
        StepPercent = "";
        RawProgressLine = "";
        Operation = Guid = AssetName = TemplateDetail = "";
        GuidSeparator = "";
        HasLiveDetail = false;
    }

    public void Feed(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string trimmed = line.Trim();

        if (ConsoleOutputLocalizer.TryResolvePhaseHeader(trimmed, out string? phaseTitle))
        {
            PhaseTitle = phaseTitle!;
            return;
        }

        if (trimmed.StartsWith("[PLAN", StringComparison.Ordinal))
        {
            return;
        }

        if (!ConsoleProgressLineParser.IsProgressLine(trimmed))
        {
            return;
        }

        string display = ConsoleOutputLocalizer.LocalizeProgressLine(trimmed);
        if (!ConsoleProgressLineParser.TrySplit(
                display, out string metrics, out string operation, out _, out string template, out string guid, out string assetName))
        {
            return;
        }

        OperationCounts = ConsoleProgressLineParser.FormatCountPair(metrics);
        StepPercent = ConsoleProgressLineParser.FormatStepPercent(metrics);
        RawProgressLine = display;
        Operation = operation;
        Guid = guid;
        AssetName = assetName;
        TemplateDetail = template;
        GuidSeparator = guid.Length > 0 && assetName.Length > 0 ? " - " : "";
        HasLiveDetail = true;
    }
}
