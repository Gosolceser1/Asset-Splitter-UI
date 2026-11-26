using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AssetProcessor;

/// <summary>
/// Merkle-style directory fingerprint: sorted relative paths (forward slashes) each paired with a
/// streamed SHA-256 of that file's bytes, then SHA-256 of the concatenated <c>path\0fileHash\n</c> lines.
/// Missing/extra/edited files all change the digest.
/// </summary>
internal static class DirectoryTreeFingerprint
{
    /// <summary>
    /// Computes a lowercase hex tree digest for every file under <paramref name="root"/>.
    /// Files whose bare names are in <paramref name="excludeFileNames"/> are skipped (stamp files).
    /// Returns null when the root is missing or I/O fails.
    /// </summary>
    public static string? TryCompute(string root, params string[] excludeFileNames) =>
        TryCompute(root, onProgress: null, excludeFileNames);

    public static string? TryCompute(
        string root,
        Action<int, int>? onProgress,
        params string[] excludeFileNames)
    {
        Dictionary<string, string>? map = TryComputeFileHashes(root, onProgress, excludeFileNames);
        return map is null ? null : DigestFromFileHashes(map);
    }

    /// <summary>
    /// Relative path → file SHA-256 hex for every file under <paramref name="root"/> (exclusions applied).
    /// Returns null when the root is missing or I/O fails; empty dictionary for an empty directory.
    /// </summary>
    public static Dictionary<string, string>? TryComputeFileHashes(string root, params string[] excludeFileNames) =>
        TryComputeFileHashes(root, onProgress: null, excludeFileNames);

    public static Dictionary<string, string>? TryComputeFileHashes(
        string root,
        Action<int, int>? onProgress,
        params string[] excludeFileNames)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        HashSet<string>? exclude = excludeFileNames.Length == 0
            ? null
            : new HashSet<string>(excludeFileNames, StringComparer.OrdinalIgnoreCase);

        try
        {
            List<string> files = EnumerateFiles(root, exclude);
            int total = files.Count;
            if (total == 0)
            {
                onProgress?.Invoke(0, 0);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            onProgress?.Invoke(0, total);

            var map = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int processed = 0;
            int failed = 0;
            var progressLock = new Lock();

            Parallel.ForEach(
                files,
                file =>
                {
                    try
                    {
                        string relative = NormalizeRelative(Path.GetRelativePath(root, file));
                        if (relative.Length == 0)
                        {
                            return;
                        }

                        string? fileHash = TryComputeFileSha256HexWithRetry(file, out bool vanished);
                        if (vanished)
                        {
                            // Deleted between enumeration and hashing: it is simply not part of the tree.
                            return;
                        }

                        if (fileHash is null)
                        {
                            Interlocked.Exchange(ref failed, 1);
                            return;
                        }

                        map[relative] = fileHash;
                    }
                    finally
                    {
                        ReportProgress(ref processed, total, onProgress, progressLock);
                    }
                });

            if (failed != 0)
            {
                return null;
            }

            onProgress?.Invoke(total, total);
            return new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Hashes <paramref name="file"/>, retrying briefly on failure: antivirus, backup or cloud-sync
    /// tools often hold short sharing locks, and a single failed file makes the whole tree map null
    /// (which forces a Full rebuild or stamps a blank digest).
    /// </summary>
    private static string? TryComputeFileSha256HexWithRetry(string file, out bool vanished)
    {
        vanished = false;
        int[] delaysMs = [0, 100, 300];
        foreach (int delay in delaysMs)
        {
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }

            string? hash = TryComputeFileSha256Hex(file);
            if (hash is not null)
            {
                return hash;
            }

            if (!File.Exists(file))
            {
                vanished = true;
                return null;
            }
        }

        return null;
    }

    /// <summary>Stable digest of a path→hash map (order-independent via sorted keys).</summary>
    public static string DigestFromFileHashes(IReadOnlyDictionary<string, string> fileHashes)
    {
        var pairs = fileHashes
            .Select(static kv => (Relative: NormalizeRelative(kv.Key), FileHash: kv.Value.Trim().ToLowerInvariant()))
            .Where(static p => p.Relative.Length > 0 && p.FileHash.Length > 0)
            .OrderBy(static p => p.Relative, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string relative, string fileHash) in pairs)
        {
            hasher.AppendData(Encoding.UTF8.GetBytes(relative));
            hasher.AppendData([(byte)0]);
            hasher.AppendData(Encoding.UTF8.GetBytes(fileHash));
            hasher.AppendData([(byte)'\n']);
        }

        return Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Builds a subtree digest from a parent tree path→hash map without re-reading files.
    /// <paramref name="subtreeRelativeRoot"/> is the folder relative to the tree root
    /// (e.g. one mod package under the mods root).
    /// </summary>
    public static string? TryDigestSubtree(
        IReadOnlyDictionary<string, string> treeHashes,
        string subtreeRelativeRoot)
    {
        string prefix = NormalizeRelative(subtreeRelativeRoot);
        if (prefix.Length == 0 || treeHashes.Count == 0)
        {
            return null;
        }

        string prefixSlash = prefix + "/";
        var subset = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string path, string hash) in treeHashes)
        {
            string normalized = NormalizeRelative(path);
            if (normalized.StartsWith(prefixSlash, StringComparison.OrdinalIgnoreCase))
            {
                subset[normalized[prefixSlash.Length..]] = hash;
            }
        }

        return subset.Count == 0 ? null : DigestFromFileHashes(subset);
    }

    /// <summary>
    /// Classifies path-level differences between two path→hash maps (added / removed / content-changed).
    /// </summary>
    public static void DiffFileHashes(
        IReadOnlyDictionary<string, string>? expected,
        IReadOnlyDictionary<string, string>? actual,
        List<string> added,
        List<string> removed,
        List<string> changed)
    {
        expected ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        actual ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string path, string hash) in actual)
        {
            if (!expected.TryGetValue(path, out string? previous))
            {
                added.Add(path);
                continue;
            }

            if (!previous.Equals(hash, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(path);
            }
        }

        foreach (string path in expected.Keys)
        {
            if (!actual.ContainsKey(path))
            {
                removed.Add(path);
            }
        }

        added.Sort(StringComparer.OrdinalIgnoreCase);
        removed.Sort(StringComparer.OrdinalIgnoreCase);
        changed.Sort(StringComparer.OrdinalIgnoreCase);
    }

    public static string? TryComputeFileSha256Hex(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(path);
            byte[] hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lists normalized relative paths under <paramref name="root"/> without hashing file contents.
    /// </summary>
    public static List<string> ListRelativePaths(string root, params string[] excludeFileNames)
    {
        var paths = new List<string>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return paths;
        }

        HashSet<string>? exclude = excludeFileNames.Length == 0
            ? null
            : new HashSet<string>(excludeFileNames, StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (string file in EnumerateFiles(root, exclude))
            {
                string relative = NormalizeRelative(Path.GetRelativePath(root, file));
                if (relative.Length > 0)
                {
                    paths.Add(relative);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return paths;
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);
        return paths;
    }

    /// <summary>
    /// Whether to emit a progress tick while hashing <paramref name="total"/> files.
    /// Reports first, last, small totals, and every N files (default 100, or 500 when total ≥ 5000).
    /// </summary>
    public static bool ShouldReportHashProgress(int processed, int total, int interval = 0)
    {
        if (total <= 0)
        {
            return processed == 0;
        }

        if (processed <= 1 || processed >= total || total <= 5)
        {
            return true;
        }

        int step = interval > 0
            ? interval
            : total >= 5_000
                ? 500
                : 100;
        return step > 0 && processed % step == 0;
    }

    private static List<string> EnumerateFiles(string root, HashSet<string>? exclude)
    {
        var files = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (exclude is not null && exclude.Contains(fileName))
            {
                continue;
            }

            files.Add(file);
        }

        return files;
    }

    private static void ReportProgress(
        ref int processed,
        int total,
        Action<int, int>? onProgress,
        Lock progressLock)
    {
        int current = Interlocked.Increment(ref processed);
        if (onProgress is null || !ShouldReportHashProgress(current, total))
        {
            return;
        }

        lock (progressLock)
        {
            onProgress(current, total);
        }
    }

    private static string NormalizeRelative(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
