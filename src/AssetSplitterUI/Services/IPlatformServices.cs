using System.Threading.Tasks;

namespace AssetSplitterUI.Services;

/// <summary>Represents a detected Anno game installation with its path, game ID, and UI display name.</summary>
/// <param name="Path">Full path to the game root (e.g. <c>C:\Program Files\Anno 1800</c>).</param>
/// <param name="GameType">Game identifier: <c>"anno1800"</c> or <c>"anno117"</c>.</param>
/// <param name="DisplayName">Human-readable label for dropdowns (e.g. <c>"Anno 117 - Pax Romana"</c>).</param>
public record GameInstallation(string Path, string GameType, string DisplayName);

/// <summary>Platform-specific services: folder picker, game detection, app-data path, and OS flags.</summary>
public interface IPlatformServices
{
    /// <summary>Shows a native folder-picker dialog and returns the selected path, or <see langword="null"/> when cancelled.</summary>
    /// <param name="title">Dialog title text.</param>
    /// <param name="initialPath">Directory to open initially; ignored when <see langword="null"/> or missing.</param>
    Task<string?> ShowFolderPickerAsync(string title, string? initialPath = null);

    /// <summary>Scans the registry, common paths, and Steam libraries for Anno installations.</summary>
    /// <returns>Deduplicated list of located game installations.</returns>
    Task<List<GameInstallation>> DetectGameInstallationsAsync();

    /// <summary>Validates a manually chosen folder and resolves Anno game subfolders when needed.</summary>
    IReadOnlyList<GameInstallation> RecognizeGameInstallations(string path);

    /// <summary>Opens <paramref name="path"/> in the system file manager (Explorer / xdg-open / open).</summary>
    Task OpenFolderAsync(string path);

    /// <summary>Returns the application data folder path for persisting settings, creating it if needed.</summary>
    string GetAppDataPath();

    /// <summary><see langword="true"/> when running on Windows.</summary>
    bool IsWindows { get; }

    /// <summary><see langword="true"/> when running on Linux.</summary>
    bool IsLinux { get; }

    /// <summary><see langword="true"/> when running on macOS.</summary>
    bool IsMacOS { get; }
}
