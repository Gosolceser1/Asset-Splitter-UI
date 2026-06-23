using System.Threading;
using System.Threading.Tasks;
namespace AssetSplitterUI.Services;

public sealed class ExtractionResult
{
    public enum StatusKind { Success, Cancelled, Error }
    public StatusKind Status { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class ExtractionCoordinator
{
    private CancellationTokenSource? _cts;

    public bool IsRunning => _cts != null;

    public CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    public async Task<ExtractionResult> RunAsync(
        AssetProcessorRunConfig config,
        Action<double> onProgress,
        Action<string> onLogLine)
    {
        _cts = new CancellationTokenSource();
        try
        {
            await AssetProcessorRunner.RunAsync(config, onProgress, onLogLine, _cts.Token);
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
            CancellationTokenSource? cts = _cts;
            _cts = null;
            cts?.Dispose();
        }
    }

    public void Cancel(bool fastKill = false)
    {
        _cts?.Cancel();
        AssetProcessorRunner.TryKillCurrentProcess(fastKill ? 0 : 500);
    }
}
