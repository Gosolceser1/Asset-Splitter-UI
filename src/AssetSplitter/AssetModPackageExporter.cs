using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace AssetProcessor;

public static partial class AssetModPackageExporter
{
    private const int MaxFolderNameLength = 90;
    private const string FlatModIndexKey = "";

    public static AssetModPackageExportResult Export(PipelineContext context, string gameType)
    {
        string sourceRoot = context.AssetOutputDir;
        string outputRoot = context.IsSingleAssetMode
            ? context.SingleAssetModOutputRoot
            : context.AssetModOutputRoot;

        if (!Directory.Exists(sourceRoot))
        {
            return new AssetModPackageExportResult(outputRoot, 0, 0, Prepared: false);
        }

        if (!OutputDirectoryManager.TryPrepareModOutputDirectory(context))
        {
            context.Issues.ReportFatal(
                PipelineIssueCodes.PipelineFatalError,
                string.Format(ConsoleMessages.Get("modOutputFolderPrepareFailed"), outputRoot, "prepare failed"),
                ConsoleMessages.Get("issueModPrepareRootCause"),
                ConsoleMessages.Get("issueModPrepareHint"),
                outputRoot);
            return new AssetModPackageExportResult(outputRoot, 0, 0, Prepared: false);
        }

        string[] files = GetFilesToPackage(context, sourceRoot)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (context.PreserveOutputDirectories
            && context.RepairPackageGuids is { Count: > 0 })
        {
            files = files
                .Where(file =>
                {
                    Match match = AssetFileNameRegex().Match(Path.GetFileName(file));
                    return match.Success && context.RepairPackageGuids.Contains(match.Groups["guid"].Value);
                })
                .ToArray();
        }

        context.Log.WriteLocalized(ConsoleMessageKind.Phase, "phase8AssetModPackages", always: true);
        if (context.IsSingleAssetMode)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Assets, "assetModsCreatingSingle", always: true, outputRoot);
        }
        else
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Assets, "assetModsCreatingMany", always: true, outputRoot);
        }

        int created = 0;
        int skipped = 0;
        var usedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexes = new Dictionary<string, List<AssetModIndexEntry>>(StringComparer.OrdinalIgnoreCase);

        if (context.DebugMode)
        {
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugModExportSource"), sourceRoot));
            context.Log.Debug(string.Format(ConsoleMessages.Get("debugModExportTarget"), outputRoot));
        }

        context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetModsProcessingFiles"), files.Length.ToString("N0")));

        HashSet<string>? existingModGuids = null;
        if (context.PreserveOutputDirectories)
        {
            existingModGuids = IndexExistingModGuids(outputRoot);
            if (context.RepairPackageGuids is { Count: > 0 })
            {
                existingModGuids.ExceptWith(context.RepairPackageGuids);
            }
        }

        foreach (string file in files)
        {
            context.ThrowIfCancellationRequested();

            if (existingModGuids is not null
                && TryReadAssetMetadata(context, file, out AssetModMetadata existingMeta)
                && existingModGuids.Contains(existingMeta.Guid))
            {
                skipped++;
                continue;
            }

            if (context.PreserveOutputDirectories
                && context.RepairPackageGuids is { Count: > 0 }
                && TryReadAssetMetadata(context, file, out AssetModMetadata rebuildMeta)
                && context.RepairPackageGuids.Contains(rebuildMeta.Guid))
            {
                DeleteModFoldersForGuid(context, outputRoot, rebuildMeta.Guid);
            }

            bool packageCreated;
            string? writtenModAssetsPath = null;
            try
            {
                packageCreated = TryCreatePackage(
                    context,
                    gameType,
                    sourceRoot,
                    outputRoot,
                    file,
                    usedDirectories,
                    indexes,
                    out writtenModAssetsPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                context.Issues.ReportModPackageReadFailed(file, ex.Message);
                context.Log.WriteLocalized(ConsoleMessageKind.Warning, "assetModsReadXmlWarning", always: true, file, ex.Message);
                packageCreated = false;
            }

            if (!packageCreated)
            {
                if (context.DebugMode)
                {
                    context.Log.Debug(string.Format(ConsoleMessages.Get("debugAssetModsSkippedInvalidMetadata"), Path.GetFileName(file)));
                }

                context.DebugStats.RecordModPackageSkipped();
                skipped++;
                continue;
            }

            created++;
            context.DebugStats.RecordModPackageCreated();
            TryLogModRepairReason(context, file, writtenModAssetsPath);
            if (ShouldReportModProgressIntermediate(context, created, files.Length))
            {
                string? templateName = context.DebugMode
                    ? null
                    : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(file);
                string modProgress = context.DebugMode
                    ? ConsoleMessages.Get("assetModsProgress")
                    : AssetProgressFormatter.FromAssetFileStem("Creating mod", Path.GetFileNameWithoutExtension(file), templateName);
                context.ProgressReporter.ReportProgress(modProgress, created.ToString(), files.Length.ToString());
            }
        }

        if (files.Length > 0)
        {
            context.ProgressReporter.ReportProgress(ConsoleMessages.Get("assetModsProgress"), created.ToString(), files.Length.ToString());
        }

        if (created > 0)
        {
            ModReadmeWriter.WriteModdingGuide(outputRoot, gameType, context);
        }

        if (!context.IsSingleAssetMode)
        {
            if (!context.PreserveOutputDirectories)
            {
                WriteTemplateIndexes(outputRoot, indexes, context.ReadmeLanguage);
                ModReadmeWriter.WriteExportSummary(outputRoot, gameType, created, skipped, singleAssetMode: false, context);
            }
        }
        else if (created > 0)
        {
            ModReadmeWriter.WriteExportSummary(outputRoot, gameType, created, skipped, singleAssetMode: true, context);
        }
        if (context.DebugMode)
        {
            context.DebugStats.WriteModPackageSummary(context.Log);
        }

        if (context.IsSingleAssetMode)
        {
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, "assetModsCreatedSingle", always: true, outputRoot);
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "assetModsSingleXmlOutput", always: true, context.SingleAssetOutputRoot);
        }
        else
        {
            string summaryKey = context.PreserveOutputDirectories
                && context.RepairPackageGuids is { Count: > 0 }
                    ? "assetModsCreatedSummaryRepair"
                    : "assetModsCreatedSummary";
            context.Log.WriteLocalized(ConsoleMessageKind.Ok, summaryKey, always: true, created.ToString("N0"), skipped.ToString("N0"));
            context.Log.WriteLocalized(ConsoleMessageKind.Info, "assetModsOutputFolder", always: true, outputRoot);
        }
        return new AssetModPackageExportResult(outputRoot, created, skipped);
    }

    private static void TryLogModRepairReason(PipelineContext context, string file, string? writtenModAssetsPath)
    {
        Match match = AssetFileNameRegex().Match(Path.GetFileName(file));
        if (!match.Success)
        {
            return;
        }

        string guid = match.Groups["guid"].Value;
        string label = Path.GetFileNameWithoutExtension(file);
        OutputRunCache.TryWriteRepairActionLine(
            context,
            guid,
            label,
            OutputRunCache.TryMapItemKeyToModRepairActionKey,
            ConsoleMessageKind.Assets);

        if (!string.IsNullOrWhiteSpace(writtenModAssetsPath))
        {
            OutputRunCache.TryWriteContentChangedModDiff(context, guid, label, writtenModAssetsPath);
        }
    }

    private static bool ShouldReportModProgressIntermediate(PipelineContext context, int created, int total) =>
        DeveloperTrace.ShouldReportProgress(context, created, total, normalInterval: 200);

    private static HashSet<string> IndexExistingModGuids(string outputRoot)
    {
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(outputRoot))
        {
            return guids;
        }

        foreach (string modinfo in Directory.EnumerateFiles(outputRoot, "modinfo.json", SearchOption.AllDirectories))
        {
            string? folder = Path.GetDirectoryName(modinfo);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (OutputRunCache.TryParseModFolderGuid(Path.GetFileName(folder), out string guid))
            {
                guids.Add(guid);
            }
        }

        return guids;
    }

    private static void DeleteModFoldersForGuid(PipelineContext context, string outputRoot, string guid)
    {
        if (string.IsNullOrWhiteSpace(guid) || !Directory.Exists(outputRoot))
        {
            return;
        }

        List<string> toDelete = [];
        foreach (string modinfo in Directory.EnumerateFiles(outputRoot, "modinfo.json", SearchOption.AllDirectories))
        {
            string? folder = Path.GetDirectoryName(modinfo);
            if (string.IsNullOrWhiteSpace(folder))
            {
                continue;
            }

            if (OutputRunCache.TryParseModFolderGuid(Path.GetFileName(folder), out string folderGuid)
                && folderGuid.Equals(guid.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                toDelete.Add(folder);
            }
        }

        foreach (string folder in toDelete)
        {
            _ = OutputPathSafety.TryDeleteDirectoryRecursive(context, folder, context.GameOutputRoot);
        }
    }

    private static IEnumerable<string> GetFilesToPackage(PipelineContext context, string sourceRoot)
    {
        if (!context.IsSingleAssetMode)
        {
            return Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.AllDirectories);
        }

        string[] topLevelFiles = Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.TopDirectoryOnly).ToArray();
        return topLevelFiles.Length > 0
            ? topLevelFiles
            : Directory.EnumerateFiles(sourceRoot, "*.xml", SearchOption.AllDirectories);
    }
}
