using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace AssetProcessor;

/// <summary>
/// Stamps <c>source_xml_*</c> with a game-build fingerprint and a content tree hash so full
/// processing runs can skip wipe+RDA extract only when the install build is unchanged <em>and</em>
/// on-disk source files still match the stamped tree. Source-only runs always force refresh.
/// Content-changed with required files still present <em>and intact</em> keeps on-disk source
/// (no RDA wipe); output cache then DiffRepair / Complete from extractable hashes.
/// Corrupt/incomplete required files (empty, not starting with <c>&lt;</c>, or XML load failure)
/// force Phase 1 RDA re-extract — never keep bad source on disk.
/// <para>
/// Schema 2: <c>contentTreeHash</c> (sorted relative path + per-file SHA-256 digest), excluding the
/// stamp file itself. Schema-1 stamps miss once and re-extract.
/// </para>
/// </summary>
internal static class SourceXmlCache
{
    public const string StampFileName = ".asset-splitter-source-cache.json";
    public const int SchemaVersion = 2;

    public enum MissReason
    {
        None,
        ForceRefresh,
        BuildUnknown,
        StampMissing,
        StampInvalid,
        BuildChanged,
        FilesIncomplete,
        /// <summary>Required files exist but fail integrity (empty / non-markup / not well-formed).</summary>
        FilesCorrupt,
        GameTypeMismatch,
        ContentChanged,
    }

    private sealed class StampDocument
    {
        [JsonPropertyName("schema")]
        public int Schema { get; set; }

        [JsonPropertyName("gameType")]
        public string GameType { get; set; } = "";

        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = "";

        [JsonPropertyName("contentTreeHash")]
        public string ContentTreeHash { get; set; } = "";

        /// <summary>Optional path→hash map for cheap content-changed path logging.</summary>
        [JsonPropertyName("fileHashes")]
        public Dictionary<string, string>? FileHashes { get; set; }

        [JsonPropertyName("display")]
        public string Display { get; set; } = "";

        [JsonPropertyName("extractedUtc")]
        public string ExtractedUtc { get; set; } = "";
    }

    public static string BuildFingerprint(GameBuildInfo build)
    {
        static string Norm(string? value) => (value ?? string.Empty).Trim();

        string[] parts =
        [
            Norm(build.PatchVersion),
            Norm(build.Branch),
            Norm(build.Changelist),
            Norm(build.BuildTime),
        ];

        return string.Join('|', parts.Where(static p => p.Length > 0));
    }

    public static bool CanReuse(
        string sourceXmlPath,
        string gameType,
        string assetLanguage,
        GameBuildInfo? detectedBuild,
        out MissReason reason)
    {
        reason = MissReason.None;

        if (detectedBuild is null)
        {
            reason = MissReason.BuildUnknown;
            return false;
        }

        string fingerprint = BuildFingerprint(detectedBuild);
        if (fingerprint.Length == 0)
        {
            reason = MissReason.BuildUnknown;
            return false;
        }

        if (!TryReadStamp(sourceXmlPath, out StampDocument? stamp) || stamp is null)
        {
            reason = MissReason.StampMissing;
            return false;
        }

        if (stamp.Schema != SchemaVersion
            || string.IsNullOrWhiteSpace(stamp.Fingerprint)
            || string.IsNullOrWhiteSpace(stamp.ContentTreeHash))
        {
            reason = MissReason.StampInvalid;
            return false;
        }

        if (!stamp.GameType.Equals(NormalizeGameType(gameType), StringComparison.OrdinalIgnoreCase))
        {
            reason = MissReason.GameTypeMismatch;
            return false;
        }

        if (!stamp.Fingerprint.Equals(fingerprint, StringComparison.Ordinal))
        {
            reason = MissReason.BuildChanged;
            return false;
        }

        if (!HasRequiredFiles(sourceXmlPath, gameType, assetLanguage))
        {
            reason = MissReason.FilesIncomplete;
            return false;
        }

        // Cheap integrity only here (happy-path CanReuse must stay fast). Full XML load
        // runs when Phase 1 considers keeping a content-changed tree on disk.
        if (!AreRequiredFilesIntact(sourceXmlPath, gameType, assetLanguage, out _))
        {
            reason = MissReason.FilesCorrupt;
            return false;
        }

        Dictionary<string, string>? actualHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
            sourceXmlPath,
            StampFileName);
        if (actualHashes is null)
        {
            reason = MissReason.FilesIncomplete;
            return false;
        }

        string actualTree = DirectoryTreeFingerprint.DigestFromFileHashes(actualHashes);
        if (!actualTree.Equals(stamp.ContentTreeHash.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            reason = MissReason.ContentChanged;
            return false;
        }

        return true;
    }

    /// <summary>
    /// When <see cref="CanReuse"/> fails with <see cref="MissReason.ContentChanged"/>, lists
    /// relative paths that differ from the stamped file-hash map (when available).
    /// </summary>
    public static IReadOnlyList<string> ListContentChangedPaths(string sourceXmlPath)
    {
        var paths = new List<string>();
        if (!TryReadStamp(sourceXmlPath, out StampDocument? stamp) || stamp?.FileHashes is null)
        {
            return paths;
        }

        Dictionary<string, string>? actual = DirectoryTreeFingerprint.TryComputeFileHashes(
            sourceXmlPath,
            StampFileName);
        var added = new List<string>();
        var removed = new List<string>();
        var changed = new List<string>();
        DirectoryTreeFingerprint.DiffFileHashes(stamp.FileHashes, actual, added, removed, changed);
        paths.AddRange(changed);
        paths.AddRange(added);
        paths.AddRange(removed);
        return paths;
    }

    public static bool HasRequiredFiles(string sourceXmlPath, string gameType, string assetLanguage)
    {
        if (string.IsNullOrWhiteSpace(sourceXmlPath) || !Directory.Exists(sourceXmlPath))
        {
            return false;
        }

        foreach (string fileName in SourceXmlCatalog.GetExpectedFileNames(gameType, assetLanguage))
        {
            if (!File.Exists(Path.Combine(sourceXmlPath, fileName)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Cheap integrity: every required file is non-empty and starts with <c>&lt;</c> (after optional BOM).
    /// </summary>
    public static bool AreRequiredFilesIntact(
        string sourceXmlPath,
        string gameType,
        string assetLanguage,
        out string? failedFileName)
    {
        failedFileName = null;
        if (!HasRequiredFiles(sourceXmlPath, gameType, assetLanguage))
        {
            return false;
        }

        foreach (string fileName in SourceXmlCatalog.GetExpectedFileNames(gameType, assetLanguage))
        {
            string path = Path.Combine(sourceXmlPath, fileName);
            if (!OutputRunCache.IsIntactOutputXml(path))
            {
                failedFileName = fileName;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Full integrity for keep-on-disk decisions: cheap probe plus well-formed XML load
    /// for each required file (refuses truncated / broken markup that still starts with <c>&lt;</c>).
    /// </summary>
    public static bool AreRequiredFilesWellFormed(
        string sourceXmlPath,
        string gameType,
        string assetLanguage,
        out string? failedFileName)
    {
        if (!AreRequiredFilesIntact(sourceXmlPath, gameType, assetLanguage, out failedFileName))
        {
            return false;
        }

        foreach (string fileName in SourceXmlCatalog.GetExpectedFileNames(gameType, assetLanguage))
        {
            string path = Path.Combine(sourceXmlPath, fileName);
            if (!TryLoadXmlDocument(path))
            {
                failedFileName = fileName;
                return false;
            }
        }

        failedFileName = null;
        return true;
    }

    private static bool TryLoadXmlDocument(string path)
    {
        try
        {
            var document = new XmlDocument { PreserveWhitespace = false };
            XmlDocumentLoadRetry.Load(document, path);
            return document.DocumentElement is not null;
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    public static void WriteStamp(string sourceXmlPath, string gameType, GameBuildInfo? detectedBuild, PipelineLogger? log = null)
    {
        if (string.IsNullOrWhiteSpace(sourceXmlPath) || detectedBuild is null)
        {
            return;
        }

        string fingerprint = BuildFingerprint(detectedBuild);
        if (fingerprint.Length == 0)
        {
            return;
        }

        try
        {
            _ = Directory.CreateDirectory(sourceXmlPath);
            Dictionary<string, string>? fileHashes = DirectoryTreeFingerprint.TryComputeFileHashes(
                sourceXmlPath,
                StampFileName);
            if (fileHashes is null)
            {
                return;
            }

            var stamp = new StampDocument
            {
                Schema = SchemaVersion,
                GameType = NormalizeGameType(gameType),
                Fingerprint = fingerprint,
                ContentTreeHash = DirectoryTreeFingerprint.DigestFromFileHashes(fileHashes),
                FileHashes = fileHashes,
                Display = detectedBuild.ToDisplayString(),
                ExtractedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };

            string json = JsonSerializer.Serialize(stamp, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(sourceXmlPath, StampFileName), json + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            log?.Debug(string.Format(ConsoleMessages.Get("debugSourceXmlCacheWriteFailed"), ex.Message));
        }
    }

    private static bool TryReadStamp(string sourceXmlPath, out StampDocument? stamp)
    {
        stamp = null;
        string path = Path.Combine(sourceXmlPath, StampFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            stamp = JsonSerializer.Deserialize<StampDocument>(json);
            return stamp is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private static string NormalizeGameType(string gameType) =>
        GameTypeDetector.IsAnno117(gameType) ? GameTypeDetector.Anno117 : GameTypeDetector.Anno1800;
}
