using System.Text;
using System.Xml;

namespace AssetProcessor;

internal static class AssetProcessorFileSystem
{
    /// <summary>Max bytes scanned when looking for <c>&lt;Template&gt;</c> near the top of an asset XML file.</summary>
    private const int TemplateHeaderScanBytes = 16 * 1024;

    public static string CombineSourceXml(PipelineContext context, string fileName)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Path.Combine(context.SourceXmlFolder, fileName);
    }

    public static string[] FileList(string root, string filter = "*.xml")
    {
        FileInfo[] files = new DirectoryInfo(root).GetFiles(filter);
        string[] fullPaths = new string[files.Length];

        for (int i = 0; i < files.Length; i++)
        {
            fullPaths[i] = files[i].FullName;
        }

        return fullPaths;
    }

    /// <summary>
    /// XML paths for GUID indexing: staging first, then output root (top-level only).
    /// </summary>
    public static IEnumerable<string> CollectGuidIndexFilePaths(PipelineContext context)
    {
        string stagingDir = OutputDirectoryManager.GetBaseAssetGuidStagingPath(context);
        if (Directory.Exists(stagingDir))
        {
            foreach (string path in FileList(stagingDir))
            {
                yield return path;
            }
        }

        foreach (string path in FileList(context.AssetOutputDir))
        {
            yield return path;
        }

        // With -t, unchanged assets from earlier runs live in <Template>/ subfolders. Repair runs
        // only re-extract changed GUIDs, so their parents must be found there (index is first-wins:
        // staging and fresh top-level files above take precedence).
        if (context.CreateTemplateFolders && Directory.Exists(context.AssetOutputDir))
        {
            string stagingFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingDir));
            foreach (string subdirectory in Directory.EnumerateDirectories(context.AssetOutputDir))
            {
                if (string.Equals(
                        Path.TrimEndingDirectorySeparator(Path.GetFullPath(subdirectory)),
                        stagingFull,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (string path in Directory.EnumerateFiles(subdirectory, "*.xml", SearchOption.AllDirectories))
                {
                    yield return path;
                }
            }
        }
    }

    public static IEnumerable<string> CollectGuidIndexFilePaths(string assetOut, string stagingFolderName = "BaseAssetGUID")
    {
        string stagingDir = Path.Combine(assetOut, stagingFolderName);
        if (Directory.Exists(stagingDir))
        {
            foreach (string path in FileList(stagingDir))
            {
                yield return path;
            }
        }

        foreach (string path in FileList(assetOut))
        {
            yield return path;
        }
    }

    public static string? FindAssetFile(string assetOutputDirectory, string guid, bool searchTemplateFolders, string? stagingFolderName = null)
    {
        if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(assetOutputDirectory))
        {
            return null;
        }

        string prefix = guid + " - [ ";
        string? match = FindAssetFileWithPrefix(assetOutputDirectory, prefix, SearchOption.TopDirectoryOnly);
        if (match is not null)
        {
            return match;
        }

        string stagingDirectory = Path.Combine(assetOutputDirectory, string.IsNullOrWhiteSpace(stagingFolderName) ? OutputStructureSettings.DefaultBaseAssetFolder : stagingFolderName);
        if (Directory.Exists(stagingDirectory))
        {
            match = FindAssetFileWithPrefix(stagingDirectory, prefix, SearchOption.TopDirectoryOnly);
            if (match is not null)
            {
                return match;
            }
        }

        if (searchTemplateFolders)
        {
            return FindAssetFileWithPrefix(assetOutputDirectory, prefix, SearchOption.AllDirectories);
        }

        return null;
    }

    private static string? FindAssetFileWithPrefix(string directory, string fileNamePrefix, SearchOption searchOption)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.xml", searchOption))
        {
            if (Path.GetFileName(file).StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    public static string ExtractDisplayName(string fileNameWithoutExtension)
    {
        int bracketStart = fileNameWithoutExtension.IndexOf("[ ", StringComparison.Ordinal);
        int bracketEnd = fileNameWithoutExtension.LastIndexOf(" ]", StringComparison.Ordinal);

        if (bracketStart >= 0 && bracketEnd > bracketStart)
        {
            return fileNameWithoutExtension.Substring(bracketStart + 2, bracketEnd - bracketStart - 2).Trim();
        }

        return fileNameWithoutExtension;
    }

    /// <summary>
    /// Reads <c>&lt;Template&gt;</c> from the start of an asset XML file.
    /// Scans at most <see cref="TemplateHeaderScanBytes"/> — never the full file.
    /// </summary>
    public static string? TryReadTemplateFromAssetFile(string assetFilePath)
    {
        try
        {
            using FileStream stream = new(assetFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int toRead = (int)Math.Min(TemplateHeaderScanBytes, Math.Max(0, stream.Length));
            if (toRead == 0)
            {
                return null;
            }

            byte[] buffer = new byte[toRead];
            int read = stream.Read(buffer, 0, toRead);
            if (read <= 0)
            {
                return null;
            }

            string header = Encoding.UTF8.GetString(buffer, 0, read);
            int templateStart = header.IndexOf("<Template>", StringComparison.Ordinal);
            if (templateStart < 0)
            {
                return null;
            }

            int templateEnd = header.IndexOf("</Template>", templateStart, StringComparison.Ordinal);
            if (templateEnd <= templateStart)
            {
                return null;
            }

            string value = header.Substring(templateStart + 10, templateEnd - templateStart - 10).Trim();
            return value.Length > 0 ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or DecoderFallbackException)
        {
            return null;
        }
    }

    public static string? TryReadTemplateFromAssetXml(XmlDocument document) =>
        document.SelectSingleNode("//Asset/Template")?.InnerText?.Trim()
        ?? document.SelectSingleNode("//Template")?.InnerText?.Trim();

    public static bool IsDirectoryEmpty(string path)
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }
}
