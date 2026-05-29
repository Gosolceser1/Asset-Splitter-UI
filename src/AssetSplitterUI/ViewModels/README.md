# ViewModels

MVVM layer for the Avalonia UI ([CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)).

## Main window

| Type | Purpose |
|------|---------|
| `MainWindowViewModel` | Paths, options, progress, status, run/cancel — split across partial classes (see [AssetSplitterUI/README.md](../README.md)) |
| `LocalizedTextState` | Status text that re-resolves when UI language changes |
| `ConsoleOutputCoordinator` | Queues backend lines into `MainWindowLogStore` |
| `GameConsoleState` | Saved log/progress when switching detected games |
| `BusyIndicatorAnimator` | Processing spinner / ellipsis animation |

## Log display

| Type | Purpose |
|------|---------|
| `LogLine` | One console row (text, `LogLineKind`, optional localization metadata) |
| Classification | `Services/ConsoleLineClassifier` |
| Progress parsing | `Services/ConsoleProgressLineParser` |

`MainWindowLogStore` lives in **Services/** (not ViewModels) but is owned by the main VM.

## Conventions

- Commands: `[RelayCommand]` on partial class methods.
- Settings and detection logic stay in partials — avoid growing a single `.cs` file.
- Backend work runs off the UI thread; progress/log updates use `Dispatcher.UIThread`.
