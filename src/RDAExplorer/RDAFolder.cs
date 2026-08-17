namespace RDAExplorer;

/// <summary>
/// In-memory tree of an RDA archive: folders and files. Built from a flat file list after reading the RDA.
/// Used to browse and extract files by path (e.g. "data/config/export/assets.xml").
/// </summary>
public class RDAFolder
{
    /// <summary>RDA format version for this tree.</summary>
    public FileHeader.RdaVersion Version { get; set; }
    /// <summary>Full logical path of this folder (e.g. "data/config").</summary>
    public string FullPath { get; set; } = "";
    /// <summary>Folder name only (one segment).</summary>
    public string Name { get; set; } = "";
    /// <summary>Files directly in this folder.</summary>
    public List<RDAFile> Files { get; } = [];
    /// <summary>Subfolders.</summary>
    public List<RDAFolder> Folders { get; } = [];
    /// <summary>Parent folder; <see langword="null"/> for the root.</summary>
    public RDAFolder? Parent { get; }

    /// <summary>Creates the root folder for the given RDA version.</summary>
    public RDAFolder(FileHeader.RdaVersion version)
    {
        this.Version = version;
        this.Parent = null;
    }

    /// <summary>Creates a child folder under <paramref name="parent"/>; inherits version.</summary>
    public RDAFolder(RDAFolder parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        this.Parent = parent;
        this.Version = parent.Version;
    }

    /// <summary>Returns all files in this folder and all descendant folders (flat list).</summary>
    public List<RDAFile> GetAllFiles()
    {
        List<RDAFile> allFiles = [.. this.Files];
        foreach (RDAFolder folder in this.Folders)
        {
            allFiles.AddRange(folder.GetAllFiles());
        }

        return allFiles;
    }

    /// <summary>Builds a folder tree from a flat list of RDA files using a dictionary-backed iterative builder.</summary>
    public static RDAFolder GenerateFrom(IEnumerable<RDAFile> files, FileHeader.RdaVersion version)
    {
        ArgumentNullException.ThrowIfNull(files);

        RDAFolder root = new(version);
        var folderIndex = new Dictionary<string, RDAFolder>(StringComparer.OrdinalIgnoreCase);

        foreach (RDAFile rdaFile in files)
        {
            string fileName = rdaFile.FileName.Replace('\\', '/').Trim('/');
            int lastSlash = fileName.LastIndexOf('/');
            if (lastSlash < 0)
            {
                root.Files.Add(rdaFile);
                continue;
            }

            string dirPath = fileName[..lastSlash];
            RDAFolder folder = GetOrCreateFolder(root, folderIndex, dirPath);
            folder.Files.Add(rdaFile);
        }

        return root;
    }

    private static RDAFolder GetOrCreateFolder(RDAFolder root, Dictionary<string, RDAFolder> index, string dirPath)
    {
        if (index.TryGetValue(dirPath, out RDAFolder? existing))
        {
            return existing;
        }

        ReadOnlySpan<char> span = dirPath;
        int slashIdx = span.LastIndexOf('/');
        RDAFolder parent = slashIdx < 0 ? root : GetOrCreateFolder(root, index, dirPath[..slashIdx]);

        string name = slashIdx < 0 ? dirPath : dirPath[(slashIdx + 1)..];
        var folder = new RDAFolder(parent) { Name = name, FullPath = dirPath };
        parent.Folders.Add(folder);
        index[dirPath] = folder;
        return folder;
    }
}
