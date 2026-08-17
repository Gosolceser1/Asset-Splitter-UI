namespace AssetProcessor;

/// <summary>
/// Cooperative pipeline cancellation via a cancel file (GUI) and Ctrl+C (CLI).
/// </summary>
public static class PipelineCancellation
{
    public const string CancelFileName = ".assetsplitter-cancel";

    public static string GetCancelFilePath(PipelineContext context)
    {
        string root = !string.IsNullOrWhiteSpace(context.AnnoAssetsRoot)
            ? context.AnnoAssetsRoot
            : context.BaseOutputDir;
        return Path.Combine(root, CancelFileName);
    }

    /// <summary>Creates an empty cancel file so a running pipeline can notice and stop.</summary>
    public static void RequestCancel(string annoAssetsOrBaseDir)
    {
        if (string.IsNullOrWhiteSpace(annoAssetsOrBaseDir))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(annoAssetsOrBaseDir);
            File.WriteAllBytes(Path.Combine(annoAssetsOrBaseDir, CancelFileName), []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort signal; process kill remains the fallback.
        }
    }

    public static void ClearCancelFile(PipelineContext context)
    {
        string path = GetCancelFilePath(context);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore cleanup failures.
        }
    }

    /// <summary>
    /// Attaches a <see cref="CancellationToken"/> to <paramref name="context"/> that cancels when
    /// the cancel file appears or Ctrl+C is pressed. Dispose to unregister and clean up.
    /// </summary>
    public static IDisposable Attach(PipelineContext context) => new CancellationWatch(context);

    private sealed class CancellationWatch : IDisposable
    {
        private readonly PipelineContext _context;
        private readonly CancellationTokenSource _cts;
        private readonly Timer _timer;
        private readonly ConsoleCancelEventHandler _cancelKeyHandler;
        private int _disposed;

        public CancellationWatch(PipelineContext context)
        {
            _context = context;
            _cts = new CancellationTokenSource();
            context.CancellationToken = _cts.Token;
            ClearCancelFile(context);

            _timer = new Timer(CheckCancelFile, null, dueTime: 500, period: 500);
            _cancelKeyHandler = OnCancelKeyPress;
            Console.CancelKeyPress += _cancelKeyHandler;
        }

        private void CheckCancelFile(object? state)
        {
            if (_disposed != 0 || _cts.IsCancellationRequested)
            {
                return;
            }

            try
            {
                if (File.Exists(GetCancelFilePath(_context)))
                {
                    _cts.Cancel();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
            {
                // Ignore probe failures.
            }
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Console.CancelKeyPress -= _cancelKeyHandler;
            _timer.Dispose();
            _cts.Dispose();
            ClearCancelFile(_context);
            _context.CancellationToken = CancellationToken.None;
        }
    }
}
