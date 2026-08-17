using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class ExtractionRunResultAppenderTests : IDisposable
{
    private readonly string _root;

    public ExtractionRunResultAppenderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "assetsplitter-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void ValidatePaths_ReturnsNullForWritableDirectory()
    {
        string gamePath = CreateFakeAnnoInstall();
        string outputPath = Path.Combine(_root, "output");

        string? result = ExtractionRunResultAppender.ValidatePaths(gamePath, outputPath);

        Assert.Null(result);
        Assert.True(Directory.Exists(outputPath));
    }

    [Fact]
    public void ValidatePaths_ReturnsKeyWhenOutputIsFile()
    {
        string gamePath = CreateFakeAnnoInstall();
        string outputFile = Path.Combine(_root, "output.txt");
        File.WriteAllText(outputFile, "x");

        string? result = ExtractionRunResultAppender.ValidatePaths(gamePath, outputFile);

        Assert.Equal("dialogs.invalidOutputPath", result);
    }

    [Fact]
    public void ValidatePaths_ReturnsKeyWhenOutputIsNotWritable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string gamePath = CreateFakeAnnoInstall();
        string blockedParent = Path.Combine(_root, "blocked");
        File.WriteAllText(blockedParent, "x");
        string outputPath = Path.Combine(blockedParent, "child-output");

        string? result = ExtractionRunResultAppender.ValidatePaths(gamePath, outputPath);

        Assert.Equal("dialogs.outputDirectoryNotWritable", result);
    }

    private string CreateFakeAnnoInstall()
    {
        string gamePath = Path.Combine(_root, "Anno117");
        string mainData = Path.Combine(gamePath, "maindata");
        Directory.CreateDirectory(mainData);
        File.WriteAllText(Path.Combine(mainData, "config.rda"), "fake");
        return gamePath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup for temp test directories.
        }
    }
}
