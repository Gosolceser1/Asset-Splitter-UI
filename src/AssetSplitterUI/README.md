# AssetSplitterUI

Avalonia **desktop app** for Asset Splitter: pick game and output paths, choose processing options, run **AssetProcessor.exe**, and watch progress plus a color-coded log.

## Layout

| Folder | Contents |
|--------|----------|
| [Views/](Views/) | `MainWindow.axaml` — layout, bindings, log list |
| [ViewModels/](ViewModels/) | `MainWindowViewModel` (+ partials), `LogLine`, coordinators |
| [Services/](Services/) | Process runner, settings, detection, console parsing |
| [Localization/](Localization/) | `StringResourceManager`, value converters |
| [Converters/](Converters/) | `LogLineKindConverter` (theme-aware log colors) |
| [Assets/](Assets/) | Icon and JetBrains Mono fonts |

## MainWindowViewModel partials

| File | Responsibility |
|------|----------------|
| `MainWindowViewModel.cs` | Core properties, commands, bindings |
| `.Settings.cs` / `.SettingsPersistence.cs` | Options and `settings.json` |
| `.PathsAndDetection.cs` | Game detection, paths, languages |
| `.SingleGuid.cs` | Single-GUID lookup and validation |
| `.Extraction.cs` | Run / cancel extraction |
| `.ProcessingRun.cs` | Per-run state and backend argument assembly |
| `.FirstRunSteps.cs` | First-run banner and onboarding flow |
| `.GameLanguage.cs` | Game language selection policy and availability |
| `.RunStatus.cs` | Run status presentation helpers |
| `.ProcessingTooltips.cs` | Tooltip text for processing toggle buttons |
| `.DeveloperTools.cs` | Debug mode, copy report, open folders |

## Build and run

```bash
dotnet build AssetSplitter.sln -c Debug
dotnet run --project src/AssetSplitterUI/AssetSplitterUI.csproj -c Debug
```

User-facing docs: [README.md](../../README.md).
