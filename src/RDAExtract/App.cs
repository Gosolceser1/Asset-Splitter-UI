using RDAExplorer;

namespace RDAExtract;

/// <summary>
/// RDAExtract CLI entry point. Extracts or lists files from Anno RDA archives.
/// Usage: <c>RDAExtract &lt;RDA_path&gt; &lt;match&gt; &lt;out&gt; [-n] [-d]</c>
/// Match: semicolon-separated groups; within a group, plus means AND.
/// Example: <c>.cfg;2kimages/main+icon_+.dds</c>
/// </summary>
internal sealed class App
{
    /// <summary>Parses args, then either lists or extracts from one or more RDA files into the output folder.</summary>
    /// <returns>0 on success, 1 on error or when displaying help.</returns>
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args.Any(a =>
              a.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
              a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
              a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
        {
            LongHelp();
            return 1;
        }

        bool bare = args.Any(a => a.Equals("-n", StringComparison.OrdinalIgnoreCase));
        bool listOnly = args.Any(a => a.Equals("-d", StringComparison.OrdinalIgnoreCase));

        string[] positional = [.. args.Where(a => !a.StartsWith('-'))];
        if (positional.Length < 3)
        {
            ShortHelp();
            return 1;
        }

        string sourcePath = positional[0];
        string match = positional[1];
        string outDir = Path.IsPathRooted(positional[2])
          ? positional[2]
          : Path.Combine(Directory.GetCurrentDirectory(), positional[2]);

        _ = Directory.CreateDirectory(outDir);

        string watchFile = Path.Combine(outDir, ".watch.rda");

        if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".rda", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(watchFile, "0\n0");
            ProcessSingleRda(sourcePath, outDir, match, bare, listOnly);
            return 0;
        }

        if (!Directory.Exists(sourcePath))
        {
            Console.WriteLine($"\nERROR:\n{sourcePath} not found.");
            return 1;
        }

        DirectoryInfo dirInfo = new(sourcePath);
        FileInfo[] dataSeries = dirInfo.GetFiles("data*.rda");
        FileInfo[] allRdas = dirInfo.GetFiles("*.rda");

        if (dataSeries.Length > 0)
        {
            List<DataRdaFile> ordered = dataSeries
              .Select(fi => new { File = fi, Index = ParseIndex(fi.Name) })
              .Where(x => x.Index >= 0)
              .Select(x => new DataRdaFile(x.File, x.Index))
              .OrderByDescending(x => x.Index)
              .ToList();

            int maxIdx = ordered.Count > 0 ? ordered.Max(x => x.Index) : 0;
            foreach (DataRdaFile item in ordered)
            {
                File.WriteAllText(watchFile, $"{item.Index}{Environment.NewLine}{maxIdx}");
                ProcessSingleRda(item.File.FullName, outDir, match, bare, listOnly);
            }
            return 0;
        }

        if (allRdas.Length > 0)
        {
            foreach (FileInfo file in allRdas.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                File.WriteAllText(watchFile, $"{file.Name}{Environment.NewLine}{allRdas.Length - 1}");
                ProcessSingleRda(file.FullName, outDir, match, bare, listOnly);
            }
            return 0;
        }

        Console.WriteLine($"\nERROR:\nNo .rda files found under {sourcePath}.");
        return 1;
    }

    private static void LongHelp()
    {
        Console.WriteLine("");
        Console.WriteLine("+------------------------------------------------------------------+");
        Console.WriteLine("| RDAExtract is part of the RDAExplorer Collection (c) 2022 Pogobuckel |");
        Console.WriteLine("+------------------------------------------------------------------+");
        Console.WriteLine("");
        Console.WriteLine("Syntax:");
        Console.WriteLine("RDAExtract <RDA_path> <match1[+]match2[;match3]> <out> [-n] [-d]");
        Console.WriteLine("");
        Console.WriteLine("<RDA_path>    path to a folder with .rda files (Anno 1800 maindata or Anno 117 maindata)");
        Console.WriteLine("<match1..?>   file filter (use / to extract *all* data)");
        Console.WriteLine("<out>         output folder");
        Console.WriteLine("[-n]          store files without paths");
        Console.WriteLine("[-d]          create directory listings only");
        Console.WriteLine("");
        Console.WriteLine("Example:");
        Console.WriteLine("RDAExtract data18.rda .cfg;2kimages/main+icon_+.dds c:\\rda_data");
        Console.WriteLine("");
        Console.WriteLine("This extracts all files from data18.rda that have [.cfg] OR");
        Console.WriteLine("[2kimages/main] AND [icon_] AND [.dds] in their names and paths.");
        Console.WriteLine("The files will be stored in c:\\rda_data.");
    }

    private static void ShortHelp()
    {
        var version = typeof(App).Assembly.GetName().Version;
        var versionStr = version is not null ? $"{version.Major}.{version.Minor}" : "1.0";
        Console.WriteLine("");
        Console.WriteLine("----------------------------------");
        Console.WriteLine($"RDAExtract {versionStr}, 2022 by Pogobuckel");
        Console.WriteLine("----------------------------------");
        Console.WriteLine("Syntax:");
        Console.WriteLine("RDAExtract <RDA_path> <match1[+]match2[;match3]> <out> [-n|d]");
        Console.WriteLine("");
        Console.WriteLine("RDAExtract -h for help");
        Console.WriteLine("");
    }

    private static void ProcessSingleRda(string rdaFilePath, string outDir, string match, bool bare, bool listOnly)
    {
        if (listOnly)
        {
            Console.WriteLine($"Scanning {rdaFilePath} ...");
            RDAFileExtension.ListAll(rdaFilePath, outDir);
            Console.WriteLine("Directory files created.");
        }
        else
        {
            Console.WriteLine($"Extracting from {rdaFilePath} ...");
            RDAFileExtension.ExtractAll(rdaFilePath, outDir, match, bare);
            Console.WriteLine("File(s) unpacked.");
        }
    }

    /// <summary>Parses <c>data&lt;N&gt;.rda</c> → N; returns -1 for non-matching names.</summary>
    private static int ParseIndex(string name)
    {
        string digits = Path.GetFileNameWithoutExtension(name).Replace("data", "", StringComparison.OrdinalIgnoreCase);
        return int.TryParse(digits, out int idx) ? idx : -1;
    }

    private sealed record DataRdaFile(FileInfo File, int Index);
}
