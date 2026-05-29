using AssetSplitterUI.ViewModels;

namespace AssetSplitterUI.Services;

/// <summary>Stores console snapshots by normalized game path while the app is running.</summary>
public sealed class GameConsoleStateStore
{
    private readonly Dictionary<string, GameConsoleState> _statesByGameKey = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Saves a snapshot for <paramref name="gamePath"/> when it can be normalized.</summary>
    public void Save(string? gamePath, GameConsoleState state)
    {
        string key = GetGameConsoleKey(gamePath);
        if (key.Length > 0)
        {
            _statesByGameKey[key] = state;
        }
    }

    /// <summary>Returns a snapshot for <paramref name="gamePath"/> when one has been saved.</summary>
    public bool TryGet(string? gamePath, out GameConsoleState state)
    {
        string key = GetGameConsoleKey(gamePath);
        if (key.Length == 0)
        {
            state = null!;
            return false;
        }

        return _statesByGameKey.TryGetValue(key, out state!);
    }

    private static string GetGameConsoleKey(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalizedPath = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.Length == 0 ? "" : normalizedPath;
    }
}
