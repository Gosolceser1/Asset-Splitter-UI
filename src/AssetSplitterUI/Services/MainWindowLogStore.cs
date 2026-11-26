using System.Collections.ObjectModel;
using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Services;

/// <summary>Observable console log for the main window (localized lines, snapshots, theme refresh).</summary>
internal sealed class MainWindowLogStore(Action notifyChanged)
{
    /// <summary>
    /// Keeps ListBox item count bounded so Avalonia window teardown stays fast
    /// (see AvaloniaUI/Avalonia#6660 — close cost scales with control/subscriber count).
    /// </summary>
    private const int MaxLinesRegular = 4000;
    private const int MaxLinesDeveloper = 12000;

    private readonly Action _notifyChanged = notifyChanged;
    private string? _collapseGroup;
    private int _collapseCount;
    private string? _collapseSample;

    public bool DeveloperConsoleMode { get; set; }

    private int MaxLines => DeveloperConsoleMode ? MaxLinesDeveloper : MaxLinesRegular;
    public bool IsShutdown { get; private set; }

    public ObservableCollection<LogLine> Lines { get; } = [];

    public void BeginShutdown() => IsShutdown = true;

    public void AppendRaw(string message)
    {
        if (IsShutdown)
        {
            return;
        }

        Lines.Add(LogLine.From(message));
        TrimIfNeeded();
        _notifyChanged();
    }

    public void AppendLocalized(string key, object[]? args = null)
    {
        AppendLocalizedWithoutNotify(key, args);
        _notifyChanged();
    }

    public void AppendLocalizedWithoutNotify(string key, object[]? args = null)
    {
        if (IsShutdown)
        {
            return;
        }

        string text = ConsoleOutputLocalizer.ResolveLocalized(key, args);
        Lines.Add(LogLine.FromLocalized(text, key, args));
        TrimIfNeeded();
    }

    public void AppendTranslatedBackendLine(string text)
    {
        if (IsShutdown)
        {
            return;
        }

        Lines.Add(LogLine.From(text));
        TrimIfNeeded();
    }

    public void AppendBackendLine(string original, string displayText)
    {
        if (IsShutdown)
        {
            return;
        }
        if (DeveloperConsoleMode)
        {
            AppendDeveloperLine(displayText, original);
            return;
        }

        if (IsDuplicatePhaseHeader(displayText))
        {
            return;
        }

        Lines.Add(LogLine.FromBackendOutput(original, displayText));
        TrimIfNeeded();
    }

    private bool IsDuplicatePhaseHeader(string displayText)
    {
        if (!displayText.StartsWith("=== PHASE", StringComparison.Ordinal))
        {
            return false;
        }

        for (int i = Lines.Count - 1; i >= 0; i--)
        {
            string text = Lines[i].Text.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            return text == displayText.Trim();
        }

        return false;
    }

    public void AppendDeveloperLine(string displayText, string original)
    {
        if (IsShutdown)
        {
            return;
        }

        Lines.Add(LogLine.FromBackendOutput(original, displayText));
        TrimIfNeeded();
    }

    /// <summary>One collection update per flush batch — keeps the UI thread responsive under verbose output.</summary>
    public void AppendLinesBatch(IReadOnlyList<LogLine> lines)
    {
        if (IsShutdown || lines.Count == 0)
        {
            return;
        }

        foreach (LogLine line in lines)
        {
            if (!DeveloperConsoleMode && IsDuplicatePhaseHeader(line.Text))
            {
                continue;
            }

            Lines.Add(line);
        }

        TrimIfNeeded();
    }

    public void FlushCollapsedGroup()
    {
        if (IsShutdown || _collapseGroup is null || _collapseCount <= 0)
        {
            return;
        }

        string text = _collapseCount == 1 && _collapseSample is not null
            ? _collapseSample
            : DeveloperConsoleProcessor.BuildCollapsedSummary(_collapseGroup, _collapseCount, _collapseSample ?? "");

        Lines.Add(LogLine.From(text));
        TrimIfNeeded();
        _collapseGroup = null;
        _collapseCount = 0;
        _collapseSample = null;
    }

    public void NotifyChanged() => _notifyChanged();

    public void RefreshLocalizedLines()
    {
        if (IsShutdown)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < Lines.Count; i++)
        {
            LogLine line = Lines[i];
            if (line.LocalizationKey is not null)
            {
                string text = ConsoleOutputLocalizer.ResolveLocalized(line.LocalizationKey, line.LocalizationArgs);
                Lines[i] = line.WithText(text);
                changed = true;
            }
            else if (line.OriginalText is not null)
            {
                string text = ResolveBackendDisplay(line.OriginalText);
                if (text != line.Text)
                {
                    Lines[i] = line.WithText(text);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            _notifyChanged();
        }
    }

    private static string ResolveBackendDisplay(string originalText) =>
        ConsoleOutputLocalizer.TryResolveBackendDisplay(originalText);

    public void RefreshColors()
    {
        if (IsShutdown || Lines.Count == 0)
        {
            return;
        }

        // Only realized (visible) rows are subscribed, so this is cheap even for 12k lines, and it
        // keeps each line's text, localization key and repeat count. The old approach replaced every
        // item (and skipped logs over 500 lines, leaving them in the previous theme's colors).
        foreach (LogLine line in Lines)
        {
            line.NotifyThemeChanged();
        }

        _notifyChanged();
    }

    private void TrimIfNeeded()
    {
        int overflow = Lines.Count - MaxLines;
        if (overflow <= 0)
        {
            return;
        }

        // Cap work per batch so finish/live flushes don't freeze the UI thread.
        int remove = Math.Min(overflow, 400);
        for (int i = 0; i < remove; i++)
        {
            Lines.RemoveAt(0);
        }
    }

    public IReadOnlyList<LogLine> Snapshot() => [.. Lines];

    public void Restore(IReadOnlyList<LogLine>? lines)
    {
        ClearWithoutNotify();
        if (lines is not null)
        {
            foreach (var line in lines)
            {
                Lines.Add(line);
            }
        }

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
