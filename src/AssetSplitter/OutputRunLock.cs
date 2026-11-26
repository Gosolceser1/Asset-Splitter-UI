using System.Text;

namespace AssetProcessor;

/// <summary>
/// Prevents two pipeline runs from writing the same AnnoAssets output tree concurrently.
/// </summary>
public static class OutputRunLock
{
    public const string LockFileName = ".assetsplitter-run.lock";

    /// <summary>
    /// Tries to acquire the run lock under <see cref="PipelineContext.AnnoAssetsRoot"/>.
    /// Returns an <see cref="IDisposable"/> that releases the lock, or <c>null</c> if another live run holds it.
    /// </summary>
    public static IDisposable? TryAcquire(PipelineContext context)
    {
        string root = !string.IsNullOrWhiteSpace(context.AnnoAssetsRoot)
            ? context.AnnoAssetsRoot
            : context.BaseOutputDir;
        if (string.IsNullOrWhiteSpace(root))
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockMissingOutputRoot", always: true);
            return null;
        }

        string lockPath = Path.Combine(root, LockFileName);
        try
        {
            Directory.CreateDirectory(root);

            // The lock is the open, exclusive file handle itself, held for the whole run. The OS
            // releases it when the process exits or crashes, so a leftover file never blocks a new
            // run, a recycled PID cannot keep it "held", and two runs cannot both take over a stale
            // lock (the old PID-file check raced: both read the same dead PID, both deleted/created).
            FileStream stream;
            try
            {
                stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    // Removed when the handle closes (Dispose, or process exit on Windows).
                    FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockAcquireFailed", always: true, "lock already held by another run");
                return null;
            }

            int pid = Environment.ProcessId;
            try
            {
                // PID is informational only (diagnostics); ownership is the handle.
                stream.SetLength(0);
                byte[] bytes = Encoding.UTF8.GetBytes(pid.ToString(System.Globalization.CultureInfo.InvariantCulture));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (IOException)
            {
                // Writing the informational PID failed; the handle still provides exclusion.
            }

            return new HeldLock(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockAcquireFailed", always: true, ex.Message);
            return null;
        }
    }

    private sealed class HeldLock(FileStream stream) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                // DeleteOnClose removes the lock file while it is still exclusively ours, so a run
                // that acquires next can never have its lock deleted by this one.
                stream.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort release.
            }
        }
    }
}
