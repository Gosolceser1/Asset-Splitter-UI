using System.Diagnostics;
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

            if (File.Exists(lockPath) && !TryTakeOverStaleLock(lockPath, context))
            {
                return null;
            }

            // Stale takeover left the file in place — remove so CreateNew can claim it exclusively.
            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
            }

            int pid = Environment.ProcessId;
            try
            {
                using var stream = new FileStream(
                    lockPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                byte[] bytes = Encoding.UTF8.GetBytes(pid.ToString());
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (IOException)
            {
                // Another process won the CreateNew race.
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockAcquireFailed", always: true, "lock already held");
                return null;
            }

            return new HeldLock(lockPath, pid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockAcquireFailed", always: true, ex.Message);
            return null;
        }
    }

    private static bool TryTakeOverStaleLock(string lockPath, PipelineContext context)
    {
        try
        {
            string text = File.ReadAllText(lockPath).Trim();
            if (!int.TryParse(text, out int pid) || pid <= 0)
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Warning, "runLockStaleInvalid", always: true);
                return true;
            }

            if (IsProcessAlive(pid))
            {
                context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockHeldByProcess", always: true, pid.ToString());
                return false;
            }

            context.Log.WriteLocalized(ConsoleMessageKind.Warning, "runLockStaleTakeover", always: true, pid.ToString());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Error, "runLockAcquireFailed", always: true, ex.Message);
            return false;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class HeldLock(string lockPath, int ownerPid) : IDisposable
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
                if (!File.Exists(lockPath))
                {
                    return;
                }

                // Only delete our own lock — another process may have taken over after a crash race.
                string text = File.ReadAllText(lockPath).Trim();
                if (text == ownerPid.ToString())
                {
                    File.Delete(lockPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort release.
            }
        }
    }
}
