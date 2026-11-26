using System.Threading;
using System.Threading.Tasks;
using AssetProcessor;

namespace AssetSplitterUI.Services;

public sealed class ExtractionResult
{
    public enum StatusKind { Success, Cancelled, Error }
    public StatusKind Status { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ExtractionCoordinator
{
    private readonly Lock _runLock = new();
    private CancellationTokenSource? _cts;
    private string? _cancelSignalDir;
    private int _running;

    public bool IsRunning => _cts != null;

    public CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    public async Task<ExtractionResult> RunAsync(
        AssetProcessorRunConfig config,
        Action<double> onProgress,
        Action<string> onLogLine)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return new ExtractionResult
            {
                Status = ExtractionResult.StatusKind.Error,
                ErrorMessage = "An extraction is already running.",
            };
        }

        CancellationTokenSource cts = new();
        lock (_runLock)
        {
            _cts = cts;
            _cancelSignalDir = ResolveAnnoAssetsPath(config.OutputPath);
        }

        try
        {
            await AssetProcessorRunner.RunAsync(config, onProgress, onLogLine, cts.Token);
            onProgress(100); // Ensure UI reaches 100% even if backend didn't emit a final marker
            return new ExtractionResult { Status = ExtractionResult.StatusKind.Success };
        }
        catch (OperationCanceledException)
        {
            return new ExtractionResult { Status = ExtractionResult.StatusKind.Cancelled };
        }
        catch (InvalidOperationException ex)
        {
            return new ExtractionResult { Status = ExtractionResult.StatusKind.Error, ErrorMessage = ex.Message };
        }
        catch (IOException ex)
        {
            return new ExtractionResult { Status = ExtractionResult.StatusKind.Error, ErrorMessage = ex.Message };
        }
        catch (Exception ex)
        {
            UILogger.Warning(nameof(ExtractionCoordinator), "Unexpected error during extraction run");
            UILogger.Debug(nameof(ExtractionCoordinator), ex);
            return new ExtractionResult { Status = ExtractionResult.StatusKind.Error, ErrorMessage = ex.Message };
        }
        finally
        {
            lock (_runLock)
            {
                if (ReferenceEquals(_cts, cts))
                {
                    _cts = null;
                    _cancelSignalDir = null;
                }
            }

            cts.Dispose();
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Cancel(bool fastKill = false)
    {
        string? cancelDir;
        CancellationTokenSource? cts;
        lock (_runLock)
        {
            cancelDir = _cancelSignalDir;
            cts = _cts;
        }

        if (cancelDir is not null)
        {
            PipelineCancellation.RequestCancel(cancelDir);
        }

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Run already finished.
        }

        if (fastKill)
        {
            // App shutdown: do not wait for a cooperative stop.
            AssetProcessorRunner.TryKillCurrentProcess(0);
        }

        // Normal cancel: GuiProcessRunner waits briefly for the backend to honour the cancel
        // signal (exit 130, output left consistent) and only then kills it.
    }

    private static string ResolveAnnoAssetsPath(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return outputPath;
        }

        return Path.GetFileName(Path.TrimEndingDirectorySeparator(outputPath))
            .Equals("AnnoAssets", StringComparison.OrdinalIgnoreCase)
            ? outputPath
            : Path.Combine(outputPath, "AnnoAssets");
    }
}
