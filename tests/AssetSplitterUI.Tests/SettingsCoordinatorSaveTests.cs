using System.Collections.ObjectModel;
using AssetSplitterUI.Services;

namespace AssetSplitterUI.Tests;

public sealed class SettingsCoordinatorSaveTests
{
    [Fact]
    public void Save_WithEmptyPaths_AfterCompletedSetup_KeepsProcessingPreferences()
    {
        string appData = Path.Combine(Path.GetTempPath(), "as-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(appData);
        try
        {
            var platform = new StubPlatformServices(appData);
            var store = new AppSettingsStore(platform);
            var coordinator = new SettingsCoordinator(store);

            var recentGames = new ObservableCollection<string> { @"C:\Games\Anno" };
            var recentOutputs = new ObservableCollection<string> { @"D:\Out" };

            coordinator.Save(
                gamePath: @"C:\Games\Anno",
                outputPath: @"D:\Out",
                addComments: true,
                fixDependencies: true,
                createTemplateFolders: true,
                modOpsWrap: true,
                includeDefaultProperties: true,
                splitTemplates: true,
                createAssetMods: true,
                uiLanguage: "English",
                themeMode: "Dark",
                lastGameLanguage: "english",
                recentGamePaths: recentGames,
                recentOutputPaths: recentOutputs);

            // Clear a path (theme/language save, window close, etc.) — must not wipe toggles.
            coordinator.Save(
                gamePath: "",
                outputPath: @"D:\Out",
                addComments: true,
                fixDependencies: true,
                createTemplateFolders: true,
                modOpsWrap: true,
                includeDefaultProperties: true,
                splitTemplates: true,
                createAssetMods: true,
                uiLanguage: "English",
                themeMode: "Light",
                lastGameLanguage: "english",
                recentGamePaths: recentGames,
                recentOutputPaths: recentOutputs);

            PersistedAppSettings? loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.True(loaded!.UserSetupComplete);
            Assert.True(loaded.AddComments);
            Assert.True(loaded.FixDependencies);
            Assert.True(loaded.CreateTemplateFolders);
            Assert.True(loaded.CreateAssetMods);
            Assert.Equal("Light", loaded.ThemeMode);
            Assert.Contains(@"C:\Games\Anno", loaded.RecentGamePaths);
            Assert.Equal("", loaded.LastGamePath);
            Assert.Equal(@"D:\Out", loaded.LastOutputPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(appData))
                {
                    Directory.Delete(appData, recursive: true);
                }
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    [Fact]
    public void Load_CorruptJson_KeepsBackupBeforeDefaultsAreSaved()
    {
        string appData = Path.Combine(Path.GetTempPath(), "as-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(appData);
        try
        {
            string settingsPath = Path.Combine(appData, "settings.json");
            const string corrupt = "{ \"LastGamePath\": \"C:\\\\Games\\\\Anno\", ";
            File.WriteAllText(settingsPath, corrupt);

            var store = new AppSettingsStore(new StubPlatformServices(appData));
            Assert.Null(store.Load());

            string backupPath = settingsPath + ".bad";
            Assert.True(File.Exists(backupPath));
            Assert.Equal(corrupt, File.ReadAllText(backupPath));
        }
        finally
        {
            try
            {
                if (Directory.Exists(appData))
                {
                    Directory.Delete(appData, recursive: true);
                }
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    private sealed class StubPlatformServices(string appDataPath) : IPlatformServices
    {
        public Task<string?> ShowFolderPickerAsync(string title, string? initialPath = null) =>
            Task.FromResult<string?>(null);

        public Task<List<GameInstallation>> DetectGameInstallationsAsync() =>
            Task.FromResult(new List<GameInstallation>());

        public IReadOnlyList<GameInstallation> RecognizeGameInstallations(string path) => [];

        public Task OpenFolderAsync(string path) => Task.CompletedTask;

        public Task<bool> ShowYesNoDialogAsync(string title, string message, string yesText, string noText) =>
            Task.FromResult(false);

        public string GetAppDataPath() => appDataPath;

        public bool IsWindows => true;
        public bool IsLinux => false;
        public bool IsMacOS => false;
    }
}
