using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.ViewModels;

internal sealed class ConsoleOutputCoordinator(
    MainWindowLogStore logStore,
    Action<string> setStatusTextRaw,
    Action<string, ReadOnlySpan<object>> setStatusTextLocalized)
{
    private const int LiveFlushBatchSize = 250;

    private readonly ConcurrentQueue<LogLine> _pendingLogLines = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private Timer? _logFlushTimer;
    private volatile bool _chainFlushRequested;
    private volatile bool _runFinished;

    private string? _latestStatusLine;

    public void EnqueueLine(LogLine line)
    {
        if (_runFinished)
        {
            return;
        }

        _pendingLogLines.Enqueue(line);
    }

    public void EnqueueStatusLine(string line) => Volatile.Write(ref _latestStatusLine, line);

    public void Start()
    {
        _runFinished = false;

        while (_pendingLogLines.TryDequeue(out _)) { }

        _latestStatusLine = null;
        _logFlushTimer = new Timer(_ => RequestFlush(DispatcherPriority.Background, LiveFlushBatchSize), null, 100, 100);
    }

    public void Stop(bool discardPending = false)
    {
        EndLiveFeed();

        if (discardPending)
        {
            while (_pendingLogLines.TryDequeue(out _)) { }
            _latestStatusLine = null;
            return;
        }

        if (!logStore.IsShutdown && !_runFinished)
        {
            RequestFlush(DispatcherPriority.Background, LiveFlushBatchSize);
        }
    }

    public void EndLiveFeed()
    {
        _logFlushTimer?.Dispose();
        _logFlushTimer = null;
    }

    /// <summary>
    /// Ends the run without painting the entire backlog (which freezes the window).
    /// Flushes finish milestones, then drops the rest — full trace stays in console_raw_*.log.
    /// </summary>
    public async Task FinishRunAsync(string? rawLogPath)
    {
        _runFinished = true;
        EndLiveFeed();

        await _flushGate.WaitAsync();
        try
        {
            var milestones = new List<LogLine>();
            int dropped = 0;

            while (_pendingLogLines.TryDequeue(out LogLine? line))
            {
                string text = line.OriginalText ?? line.Text;
                if (RunOutputMilestones.IsImportant(text))
                {
                    milestones.Add(line);
                }
                else
                {
                    dropped++;
                }
            }

            _latestStatusLine = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (logStore.IsShutdown)
                {
                    return;
                }

                AppendPreparedLines(milestones);

                logStore.NotifyChanged();
            }, DispatcherPriority.Send);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private void RequestFlush(DispatcherPriority priority, int maxBatch)
    {
        if (_runFinished)
        {
            return;
        }

        _ = FlushPendingLogLinesAsync(priority, maxBatch).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                UILogger.Warning(nameof(ConsoleOutputCoordinator), "Log flush task failed");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task FlushPendingLogLinesAsync(DispatcherPriority priority, int maxBatch)
    {
        if (_runFinished || (_pendingLogLines.IsEmpty && _latestStatusLine is null))
        {
            return;
        }

        if (!await _flushGate.WaitAsync(0))
        {
            _chainFlushRequested = true;
            return;
        }

        try
        {
            if (_runFinished || (_pendingLogLines.IsEmpty && _latestStatusLine is null))
            {
                return;
            }

            List<LogLine> prepared = [];
            while (prepared.Count < maxBatch && _pendingLogLines.TryDequeue(out LogLine? line))
            {
                prepared.Add(line);
            }

            string? rawStatus = Interlocked.Exchange(ref _latestStatusLine, null);
            string? status = rawStatus != null && ConsoleOutputLocalizer.FilterLine(rawStatus) != null ? rawStatus : null;

            if (prepared.Count == 0 && status is null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (logStore.IsShutdown)
                {
                    return;
                }

                AppendPreparedLines(prepared);

                if (logStore.DeveloperConsoleMode)
                {
                    logStore.FlushCollapsedGroup();
                }

                if (prepared.Count > 0)
                {
                    logStore.NotifyChanged();
                }

                ApplyStatusLine(status);
            }, priority);
        }
        finally
        {
            _flushGate.Release();

            if (!_runFinished
                && (_chainFlushRequested || !_pendingLogLines.IsEmpty || _latestStatusLine is not null))
            {
                _chainFlushRequested = false;
                RequestFlush(priority, maxBatch);
            }
        }
    }

    private void AppendPreparedLines(IReadOnlyList<LogLine> prepared)
    {
        List<LogLine> toAppend = [];
        List<(string Key, object[]? Args)> localized = [];

        foreach (LogLine line in prepared)
        {
            if (ConsoleOutputLocalizer.TryGetAutoUpdateLocalizationKey(line.Text, out string? key, out object[]? args) ||
                ConsoleOutputLocalizer.TryGetBackendConsoleLocalizationKey(line.Text, out key, out args))
            {
                localized.Add((key!, args));
                continue;
            }

            string display = ConsoleOutputLocalizer.LocalizeProgressLine(line.Text);
            string original = line.OriginalText ?? line.Text;
            toAppend.Add(LogLine.FromBackendOutput(original, display));
        }

        foreach ((string key, object[]? args) in localized)
        {
            logStore.AppendLocalizedWithoutNotify(key, args);
        }

        logStore.AppendLinesBatch(toAppend);
    }

    private void ApplyStatusLine(string? status)
    {
        if (status is null)
        {
            return;
        }

        bool regularProgressLine = !logStore.DeveloperConsoleMode
            && ConsoleProgressLineParser.IsProgressLine(status.TrimStart());

        if (regularProgressLine)
        {
            return;
        }

        if (ConsoleOutputLocalizer.TryGetAutoUpdateLocalizationKey(status, out string? sk, out object[]? sa))
        {
            setStatusTextLocalized(sk!, (sa ?? Array.Empty<object>()).AsSpan());
        }
        else if (ConsoleOutputLocalizer.TryGetBackendConsoleLocalizationKey(status, out sk, out sa))
        {
            setStatusTextLocalized(sk!, (sa ?? Array.Empty<object>()).AsSpan());
        }
        else
        {
            setStatusTextRaw(ConsoleOutputLocalizer.LocalizeProgressLine(status));
        }
    }
}

/// <summary>Backend lines that must reach the UI even when the post-run queue is dropped.</summary>
internal static class RunOutputMilestones
{
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

        if (t.StartsWith("[ISSUE_SUMMARY]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("[DEBUG][FLOW]", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("Final processing completed", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.StartsWith("  *", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("EXTRACTION COMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (t.StartsWith("Processed ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (t.Contains("Formatting summary:", StringComparison.Ordinal))
        {
            return true;
        }

        if (t.Contains("Total time:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
