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
    private readonly ConcurrentQueue<LogLine> _pendingLogLines = new();
    private Timer? _logFlushTimer;
    private volatile bool _logFlushActive;
    private string? _latestStatusLine;

    public void EnqueueLine(LogLine line) => _pendingLogLines.Enqueue(line);

    public void EnqueueStatusLine(string line) => Volatile.Write(ref _latestStatusLine, line);

    public void Start()
    {
        _latestStatusLine = null;
        _logFlushActive = true;
        _logFlushTimer = new Timer(_ => FlushPendingLogLines(), null, 100, 100);
    }

    public void Stop()
    {
        _logFlushActive = false;
        _logFlushTimer?.Dispose();
        _logFlushTimer = null;
        _ = FlushPendingLogLinesAsync(DispatcherPriority.Background);
    }

    public async Task StopAndFlushAsync()
    {
        _logFlushActive = false;
        _logFlushTimer?.Dispose();
        _logFlushTimer = null;
        await FlushPendingLogLinesAsync(DispatcherPriority.Send);
    }

    private void FlushPendingLogLines()
    {
        _ = FlushPendingLogLinesAsync(DispatcherPriority.Background).ContinueWith(t =>
        {
            if (t.IsFaulted)
                UILogger.Warning(nameof(ConsoleOutputCoordinator), "Log flush task failed");
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private Task FlushPendingLogLinesAsync(DispatcherPriority priority)
    {
        if (!_logFlushActive && _pendingLogLines.IsEmpty && _latestStatusLine == null) return Task.CompletedTask;
        if (_pendingLogLines.IsEmpty && _latestStatusLine == null) return Task.CompletedTask;

        List<LogLine> lines = [];
        while (_pendingLogLines.TryDequeue(out var line))
            lines.Add(line);

        var rawStatus = Interlocked.Exchange(ref _latestStatusLine, null);
        var status = rawStatus != null && ConsoleOutputLocalizer.FilterLine(rawStatus) != null ? rawStatus : null;

        var operation = Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var line in lines)
            {
                if (ConsoleOutputLocalizer.TryGetAutoUpdateLocalizationKey(line.Text, out var key, out var args) ||
                    ConsoleOutputLocalizer.TryGetBackendConsoleLocalizationKey(line.Text, out key, out args))
                {
                    logStore.AppendLocalizedWithoutNotify(key!, args);
                }
                else if (logStore.DeveloperConsoleMode)
                {
                    string displaySource = line.OriginalText ?? line.Text;
                    var processed = DeveloperConsoleProcessor.Process(displaySource, debugMode: true);
                    if (processed.SuppressDisplay)
                        continue;

                    string display = line.OriginalText != null
                        ? line.Text
                        : processed.Text;

                    logStore.AppendBackendLine(
                        original: displaySource,
                        displayText: ConsoleOutputLocalizer.LocalizeProgressLine(display));
                }
                else
                {
                    string original = line.OriginalText ?? line.Text;
                    logStore.AppendBackendLine(
                        original: original,
                        displayText: ConsoleOutputLocalizer.LocalizeProgressLine(line.Text));
                }
            }

            if (logStore.DeveloperConsoleMode)
                logStore.FlushCollapsedGroup();

            if (lines.Count > 0)
                logStore.NotifyChanged();

            if (status != null)
            {
                bool regularProgressLine = !logStore.DeveloperConsoleMode
                    && ConsoleProgressLineParser.IsProgressLine(status.TrimStart());

                if (!regularProgressLine)
                {
                    if (ConsoleOutputLocalizer.TryGetAutoUpdateLocalizationKey(status, out var sk, out var sa))
                        setStatusTextLocalized(sk!, (sa ?? Array.Empty<object>()).AsSpan());
                    else if (ConsoleOutputLocalizer.TryGetBackendConsoleLocalizationKey(status, out sk, out sa))
                        setStatusTextRaw(ConsoleOutputLocalizer.ResolveLocalized(sk!, sa));
                    else
                        setStatusTextRaw(ConsoleOutputLocalizer.LocalizeProgressLine(status));
                }
            }
        }, priority);

        // Avoid raw .GetTask() which can hide faults; attach explicit logging instead
        _ = operation.GetTask().ContinueWith(t =>
        {
            if (t.IsFaulted)
                UILogger.Warning(nameof(ConsoleOutputCoordinator), "UI thread dispatch for log flush failed");
        }, TaskContinuationOptions.OnlyOnFaulted);

        return operation.GetTask();
    }
}
