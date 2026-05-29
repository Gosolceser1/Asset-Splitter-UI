using System.Collections.ObjectModel;
using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Services;

/// <summary>Observable console log for the main window (localized lines, snapshots, theme refresh).</summary>
internal sealed class MainWindowLogStore(Action notifyChanged)
{
    private readonly Action _notifyChanged = notifyChanged;
    private string? _collapseGroup;
    private int _collapseCount;
    private string? _collapseSample;

    public bool DeveloperConsoleMode { get; set; }

    public ObservableCollection<LogLine> Lines { get; } = [];

    public void AppendRaw(string message)
    {
        Lines.Add(LogLine.From(message));
        _notifyChanged();
    }

    public void AppendLocalized(string key, object[]? args = null)
    {
        AppendLocalizedWithoutNotify(key, args);
        _notifyChanged();
    }

    public void AppendLocalizedWithoutNotify(string key, object[]? args = null)
    {
        var text = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Lines.Add(LogLine.FromLocalized(text, key, args));
    }

    public void AppendTranslatedBackendLine(string text) =>
        Lines.Add(LogLine.From(text));

    public void AppendBackendLine(string original, string displayText)
    {
        if (DeveloperConsoleMode)
        {
            AppendDeveloperLine(displayText, original);
            return;
        }

        if (IsDuplicatePhaseHeader(displayText))
            return;

        Lines.Add(LogLine.FromBackendOutput(original, displayText));
    }

    private bool IsDuplicatePhaseHeader(string displayText)
    {
        if (!displayText.StartsWith("=== PHASE", StringComparison.Ordinal))
            return false;

        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            string text = Lines[i].Text.Trim();
            if (text.Length == 0)
                continue;

            return text == displayText.Trim();
        }

        return false;
    }

    public void AppendDeveloperLine(string displayText, string original)
    {
        // Developer mode shows full per-item trace (mod packages, etc.) — no collapsing.
        Lines.Add(LogLine.FromBackendOutput(original, displayText));
    }

    public void FlushCollapsedGroup()
    {
        if (_collapseGroup is null || _collapseCount <= 0)
            return;

        string text = _collapseCount == 1 && _collapseSample is not null
            ? _collapseSample
            : DeveloperConsoleProcessor.BuildCollapsedSummary(_collapseGroup, _collapseCount, _collapseSample ?? "");

        Lines.Add(LogLine.From(text));
        _collapseGroup = null;
        _collapseCount = 0;
        _collapseSample = null;
    }

    public void NotifyChanged() => _notifyChanged();

    public void RefreshLocalizedLines()
    {
        bool changed = false;
        for (int i = 0; i < Lines.Count; i++)
        {
            LogLine line = Lines[i];
            if (line.LocalizationKey is not null)
            {
                var text = ConsoleOutputLocalizer.ResolveLocalized(line.LocalizationKey, line.LocalizationArgs);
                Lines[i] = line.WithText(text);
                changed = true;
            }
            else if (line.OriginalText is not null)
            {
                var text = ConsoleOutputLocalizer.LocalizeProgressLine(line.OriginalText);
                if (text != line.Text)
                {
                    Lines[i] = line.WithText(text);
                    changed = true;
                }
            }
        }

        if (changed)
            _notifyChanged();
    }

    public void RefreshColors()
    {
        List<LogLine> copy = [.. Lines];
        if (copy.Count == 0)
            return;

        Lines.Clear();
        foreach (var line in copy)
        {
            var source = line.OriginalText ?? line.Text;
            Lines.Add(LogLine.FromBackendOutput(line.OriginalText ?? source, source));
        }
    }

    public IReadOnlyList<LogLine> Snapshot() => [.. Lines];

    public void Restore(IReadOnlyList<LogLine>? lines)
    {
        ClearWithoutNotify();
        if (lines is not null)
            foreach (var line in lines)
                Lines.Add(line);
        _notifyChanged();
    }

    public void Clear()
    {
        ClearWithoutNotify();
        _notifyChanged();
    }

    public void ClearWithoutNotify()
    {
        FlushCollapsedGroup();
        Lines.Clear();
    }
}
