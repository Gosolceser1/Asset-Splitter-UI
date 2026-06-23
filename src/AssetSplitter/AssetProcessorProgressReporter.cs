using System.Threading;

namespace AssetProcessor;

/// <summary>
/// Writes structured progress to stdout (GUI parses redirected output). Also writes <c>fixer.txt</c>
/// for legacy CLI polling when stdout is interactive (not redirected).
/// Must be initialized with a PipelineContext before most operations.
/// </summary>
public class AssetProcessorProgressReporter
{
    private readonly Lock _syncRoot = new();
    private PipelineContext? _context;

    /// <summary>Initializes the reporter with the active pipeline context. Must be called once early in the pipeline.</summary>
    public void Initialize(PipelineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void OutputFixer(string operation, string currentCountText, string totalCountText)
    {
        if (!Console.IsOutputRedirected)
        {
            AppendToFixerLog(
                $"{operation}{Environment.NewLine}{currentCountText}{Environment.NewLine}{totalCountText}",
                append: false);
        }

        if (_context is null)
        {
            return;
        }

        if (!int.TryParse(currentCountText, out int currentCount) ||
            !int.TryParse(totalCountText, out int totalCount) ||
            totalCount <= 0)
        {
            return;
        }

        if (Console.IsOutputRedirected && !DeveloperTrace.ShouldEmitProgressLine(_context, currentCount, totalCount))
        {
            return;
        }

        double progressPercentage = (double)currentCount / totalCount * 100.0;
        ConsoleColor progressColor = GetProgressColor(progressPercentage);
        ConsoleColor originalColor = Console.ForegroundColor;
        Console.ForegroundColor = progressColor;

        // Developer mode: unchanged. Regular GUI (redirected stdout): keep labels for UI color coding.
        // Regular interactive CLI: short summaries on the in-place progress line.
        string displayOperation = _context.DebugMode
            ? operation
            : Console.IsOutputRedirected
                ? operation
                : SummarizeOperation(operation);
        string formattedProgress = FormatProgressLine(progressPercentage, currentCount, totalCount, displayOperation);

        if (!Console.IsOutputRedirected)
        {
            WriteInteractiveProgressLine(formattedProgress);
        }
        else
        {
            Console.WriteLine(formattedProgress);
        }

        Console.ForegroundColor = originalColor;

        if (currentCount >= totalCount && !Console.IsOutputRedirected)
        {
            WriteInteractiveDone(originalColor);
        }
    }

    /// <summary>Writes a line to <c>fixer.txt</c> for legacy CLI polling. Skipped when stdout is redirected (GUI).</summary>
    public void AppendToFixerLog(string text, bool append)
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        string baseDir = _context?.BaseOutputDir ?? "";
        if (string.IsNullOrEmpty(baseDir))
        {
            return;
        }

        lock (_syncRoot)
        {
            try
            {
                using FileStream fileStream = new(
                    Path.Combine(baseDir, "fixer.txt"),
                    append ? FileMode.Append : FileMode.Create,
                    FileAccess.Write);
                using StreamWriter streamWriter = new(fileStream, Encoding.UTF8);
                streamWriter.WriteLine(text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort only — don't break the main pipeline if we can't write the progress file
                System.Diagnostics.Debug.WriteLine($"[ProgressReporter] Failed to write fixer.txt: {ex.Message}");
            }
        }
    }

    private ConsoleColor GetProgressColor(double progressPercentage)
    {
        if (progressPercentage >= 75.0)
        {
            return ConsoleColor.Green;
        }

        return progressPercentage >= 25.0 ? ConsoleColor.Yellow : ConsoleColor.Red;
    }

    private string FormatProgressLine(double progressPercentage, int currentCount, int totalCount, string operation)
    {
        string progressPct = $"{progressPercentage,5:F1}%";
        string counts = $"{currentCount,6:N0}/{totalCount:N0}";
        return $"[{progressPct}] [{counts}] - {operation}";
    }

    private bool ShouldEmitProgressLine(int currentCount, int totalCount)
    {
        if (currentCount <= 1 || currentCount >= totalCount)
        {
            return true;
        }

        int defaultInterval = totalCount >= 5_000 ? 500 : 100;
        int interval = _context?.AppSettingsConfig?.Settings?.FileProcessing?.ProgressInterval ?? defaultInterval;
        return interval > 0 && currentCount % interval == 0;
    }

    private string SummarizeOperation(string operation)
    {
        // Debug: keep full per-asset labels on progress lines ([DEBUG] lines carry extra detail).
        // Normal mode: collapse noisy operations to short summaries.
        if (_context == null || _context.DebugMode)
        {
            return operation;
        }

        if (operation.StartsWith("Extracting:", StringComparison.Ordinal))
        {
            return ConsoleMessages.Get("extractingAssets").TrimEnd('.');
        }

        if (operation.StartsWith("Merging:", StringComparison.Ordinal))
        {
            return ConsoleMessages.Get("mergeTemplates").TrimEnd('.');
        }

        if (operation.StartsWith("Processing:", StringComparison.Ordinal))
        {
            return ConsoleMessages.Get("formattingAssets").TrimEnd('.');
        }

        if (operation.StartsWith("Resolving:", StringComparison.Ordinal))
        {
            return ConsoleMessages.Get("resolvingDependenciesSummary").TrimEnd('.');
        }

        if (operation.StartsWith("Scanning:", StringComparison.Ordinal))
        {
            return ConsoleMessages.Get("scanningDependenciesSummary").TrimEnd('.');
        }

        return operation;
    }

    private void WriteInteractiveProgressLine(string formattedProgress)
    {
        // Best-effort in-place progress bar update. Falls back gracefully in restricted terminals.
        try
        {
            if (Console.BufferWidth > 0)
            {
                Console.Write(("\r" + formattedProgress).PadRight(Console.BufferWidth - 1));
                Console.Write("\r" + formattedProgress);
            }
            else
            {
                Console.Write("\r" + formattedProgress);
            }
        }
        catch (IOException)
        {
            Console.Write("\r" + formattedProgress);
        }
    }

    private void WriteInteractiveDone(ConsoleColor originalColor)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" " + ConsoleMessages.Get("doneLabel"));
            Console.ForegroundColor = originalColor;
        }
        catch (IOException)
        {
            Console.WriteLine(" " + ConsoleMessages.Get("doneLabel"));
        }
    }
}
