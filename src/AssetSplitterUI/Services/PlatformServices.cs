using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace AssetSplitterUI.Services;

/// <summary>Cross-platform implementation of <see cref="IPlatformServices"/>.</summary>
public sealed class PlatformServices(Window? parentWindow = null) : IPlatformServices
{
    private readonly AnnoInstallationDetector _installationDetector = new();

    /// <inheritdoc/>
    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <inheritdoc/>
    public bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    /// <inheritdoc/>
    public bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    /// <inheritdoc/>
    public async Task<string?> ShowFolderPickerAsync(string title, string? initialPath = null)
    {
        if (parentWindow is null)
        {
            return null;
        }

        IStorageProvider storageProvider = parentWindow.StorageProvider;
        IStorageFolder? startLocation = null;
        if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
        {
            startLocation = await storageProvider.TryGetFolderFromPathAsync(initialPath);
        }

        IReadOnlyList<IStorageFolder> result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = startLocation,
            AllowMultiple = false
        });

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    /// <inheritdoc/>
    public async Task<List<GameInstallation>> DetectGameInstallationsAsync() =>
        await Task.Run(_installationDetector.Detect);

    /// <inheritdoc/>
    public IReadOnlyList<GameInstallation> RecognizeGameInstallations(string path) =>
        _installationDetector.RecognizeFromPath(path);

    /// <inheritdoc/>
    public async Task OpenFolderAsync(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                string? fileManager = (IsWindows, IsLinux, IsMacOS) switch
                {
                    (true, _, _) => "explorer.exe",
                    (_, true, _) => "xdg-open",
                    (_, _, true) => "open",
                    _ => null
                };

                if (fileManager is null)
                {
                    return;
                }

                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = fileManager,
                    ArgumentList = { path },
                    UseShellExecute = false
                });
            }
            catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or System.ComponentModel.Win32Exception)
            {
                UILogger.Warning(nameof(PlatformServices), "Failed to open folder in system file manager");
                UILogger.Debug(nameof(PlatformServices), ex);
            }
        });
    }

    /// <inheritdoc/>
    public string GetAppDataPath()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AssetSplitter");
        _ = Directory.CreateDirectory(path);
        return path;
    }
}
