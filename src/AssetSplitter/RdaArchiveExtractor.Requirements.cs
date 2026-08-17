using RDAExplorer;

namespace AssetProcessor;

public static partial class RdaArchiveExtractor
{
    private static string DescribeRequiredFiles(string extractFilter, PipelineContext context) =>
        string.Join(", ", GetRequiredBareFilesForEarlyStop(extractFilter, context));

    private static bool HasRequiredExtractFiles(PipelineContext context, string outputFolder, string extractFilter) =>
        GetMissingRequiredFiles(context, outputFolder, extractFilter).Count == 0;

    private static List<string> GetMissingRequiredFiles(PipelineContext context, string outputFolder, string extractFilter)
    {
        var missing = new List<string>();
        foreach (string fileName in GetRequiredBareFilesForEarlyStop(extractFilter, context))
        {
            if (!File.Exists(Path.Combine(outputFolder, fileName)))
            {
                missing.Add(fileName);
            }
        }

        return missing;
    }

    private static IReadOnlyList<string> GetRequiredBareFilesForEarlyStop(string extractFilter, PipelineContext context)
    {
        var required = new List<string>();
        foreach (string group in extractFilter.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string path = group.Trim();
            if (path.EndsWith("texts_", StringComparison.Ordinal))
            {
                if (!required.Contains("texts_english.xml", StringComparer.OrdinalIgnoreCase))
                {
                    required.Add("texts_english.xml");
                }

                continue;
            }

            int slash = path.LastIndexOf('/');
            string fileName = slash >= 0 ? path[(slash + 1)..] : path;
            if (!fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!required.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                required.Add(fileName);
            }
        }

        if (!context.AssetLanguage.Equals("none", StringComparison.OrdinalIgnoreCase)
            && !required.Contains(context.AssetLanguage, StringComparer.OrdinalIgnoreCase))
        {
            required.Add(context.AssetLanguage);
        }

        return required;
    }
}
