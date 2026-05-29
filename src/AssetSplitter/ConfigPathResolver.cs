namespace AssetProcessor;

public static class ConfigPathResolver
{
    public static string Resolve(string basePath, string subfolder, string filename)
    {
        if (Path.IsPathRooted(basePath))
        {
            var direct = Path.Combine(basePath, subfolder, filename);
            if (File.Exists(direct)) return direct;
        }
        var outputConfig = Path.Combine(AppContext.BaseDirectory, "config", subfolder, filename);
        if (File.Exists(outputConfig)) return outputConfig;
        string[] devCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "config", subfolder, filename),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "config", subfolder, filename)
        ];
        foreach (string dev in devCandidates)
        {
            var resolved = Path.GetFullPath(dev);
            if (File.Exists(resolved)) return resolved;
        }
        return outputConfig;
    }

    public static string Resolve(string subfolder, string filename)
        => Resolve("config", subfolder, filename);
}
