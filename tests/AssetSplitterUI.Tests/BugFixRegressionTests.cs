using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

/// <summary>Regression tests for fixes from the static bug review.</summary>
public sealed class BugFixRegressionTests
{
    [Fact]
    public void ParseSteamLibraryCommonFolders_ReadsEveryLibraryPath()
    {
        const string vdf = """
            "libraryfolders"
            {
                "0"
                {
                    "path"		"C:\\Program Files (x86)\\Steam"
                    "label"		""
                }
                "1"
                {
                    "path"		"D:\\Games\\Steam"
                    "apps" { "916440" "123" }
                }
            }
            """;

        IReadOnlyList<string> folders = AnnoInstallationDetector.ParseSteamLibraryCommonFolders(vdf);

        Assert.Equal(2, folders.Count);
        Assert.Equal(Path.Combine(@"C:\Program Files (x86)\Steam", "steamapps", "common"), folders[0]);
        Assert.Equal(Path.Combine(@"D:\Games\Steam", "steamapps", "common"), folders[1]);
    }

    [Fact]
    public void AppSettingsStore_SaveThenLoad_RoundTrips()
    {
        string appData = Path.Combine(Path.GetTempPath(), "as-settings-rt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(appData);
        try
        {
            var store = new AppSettingsStore(new FixedAppDataPlatform(appData));
            store.Save(new PersistedAppSettings { LastGamePath = @"C:\Games\Anno 1800", ThemeMode = "Dark" });

            PersistedAppSettings? loaded = store.Load();

            Assert.NotNull(loaded);
            Assert.Equal(@"C:\Games\Anno 1800", loaded!.LastGamePath);
            Assert.Equal("Dark", loaded.ThemeMode);
            Assert.Empty(Directory.GetFiles(appData, "*.tmp"));
        }
        finally
        {
            try
            {
                Directory.Delete(appData, recursive: true);
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    private sealed class FixedAppDataPlatform(string appDataPath) : IPlatformServices
    {
        public Task<string?> ShowFolderPickerAsync(string title, string? initialPath = null) => Task.FromResult<string?>(null);

        public Task<List<GameInstallation>> DetectGameInstallationsAsync() => Task.FromResult(new List<GameInstallation>());

        public IReadOnlyList<GameInstallation> RecognizeGameInstallations(string path) => [];

        public Task OpenFolderAsync(string path) => Task.CompletedTask;

        public Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText, string noText) => Task.FromResult(false);

        public string GetAppDataPath() => appDataPath;

        public bool IsWindows => true;
        public bool IsLinux => false;
        public bool IsMacOS => false;
    }
}
