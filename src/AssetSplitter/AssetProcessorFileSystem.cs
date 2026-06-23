using System.Text;
using System.Xml;

namespace AssetProcessor;

internal static class AssetProcessorFileSystem
{
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

        foreach (string path in FileList(context.AssetOut))
        {
            yield return path;
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

    public static string? FindAssetFile(string assetOutputDirectory, string guid, bool searchTemplateFolders)
    {
        string searchPattern = guid + " - [ *.xml";

        string[] files = Directory.GetFiles(assetOutputDirectory, searchPattern, SearchOption.TopDirectoryOnly);
        if (files.Length > 0)
        {
            return files[0];
        }

        string stagingDirectory = Path.Combine(assetOutputDirectory, OutputStructureSettings.DefaultBaseAssetFolder);
        if (Directory.Exists(stagingDirectory))
        {
            files = Directory.GetFiles(stagingDirectory, searchPattern, SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                return files[0];
            }
        }

        if (searchTemplateFolders)
        {
            files = Directory.GetFiles(assetOutputDirectory, searchPattern, SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0];
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

    /// <summary>Reads <c>&lt;Template&gt;</c> from an asset XML file (header scan only when possible).</summary>
    public static string? TryReadTemplateFromAssetFile(string assetFilePath)
    {
        try
        {
            using StreamReader reader = new(assetFilePath, Encoding.UTF8, true, 4096);
            string header = reader.ReadToEnd();
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
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
