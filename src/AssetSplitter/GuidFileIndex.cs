using System.Collections.Concurrent;
using System.Threading;

namespace AssetProcessor;

public sealed class GuidFileIndex
{
    private readonly Lock _lock = new();
    private Dictionary<string, string>? _index;

    public void Build(IEnumerable<string> filePaths, PipelineContext? context = null)
    {
        lock (_lock)
        {
            _index ??= new(StringComparer.OrdinalIgnoreCase);
            var paths = filePaths.ToList();
            int total = paths.Count;
            int processed = 0;
            int indexed = 0;
            int skipped = 0;

            if (context?.DebugMode == true)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidStartingBuild"), total));
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidScanRoot"), context.AssetOut));
            }
            else if (context != null)
            {
                context.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidStartingBuild"), total));
            }

            foreach (string filePath in paths)
            {
                string filename = Path.GetFileNameWithoutExtension(filePath);
                processed++;
                if (context != null && DeveloperTrace.ShouldReportProgress(context, processed, total, normalInterval: 5000))
                {
                    string? templateName = context.DebugMode
                        ? null
                        : AssetProcessorFileSystem.TryReadTemplateFromAssetFile(filePath);
                    string indexProgress = context.DebugMode
                        ? ConsoleMessages.Get("buildingGuidIndex")
                        : AssetProgressFormatter.FromAssetFileStem("Indexing", filename, templateName);
                    context.ProgressReporter.OutputFixer(indexProgress, processed.ToString(), total.ToString());
                }
                int dashIndex = filename.IndexOf(" - ", StringComparison.Ordinal);
                if (dashIndex > 0)
                {
                    string guid = filename[..dashIndex];
                    if (_index.TryAdd(guid, filePath))
                    {
                        indexed++;
                        if (context?.DebugMode == true)
                        {
                            context.Log.Debug(string.Format(
                                ConsoleMessages.Get("debugGuidIndexedFile"),
                                guid,
                                filePath));
                        }
                    }
                    else if (context?.DebugMode == true)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugGuidDuplicateSkipped"),
                            guid,
                            filePath));
                    }
                }
                else
                {
                    skipped++;
                    if (context?.DebugMode == true)
                    {
                        context.Log.Debug(string.Format(
                            ConsoleMessages.Get("debugGuidSkippedFile"),
                            filePath,
                            "filename missing 'GUID - ' pattern"));
                    }
                }
            }

            context?.Log.Debug(string.Format(ConsoleMessages.Get("debugGuidFinishedIndexing"), indexed, skipped, total));
        }
    }

    public string? Find(string guid)
    {
        if (_index == null)
            return null;

        lock (_lock)
        {
            return _index.TryGetValue(guid, out string? filePath) ? filePath : null;
        }
    }

    public int Count => _index?.Count ?? 0;
}
