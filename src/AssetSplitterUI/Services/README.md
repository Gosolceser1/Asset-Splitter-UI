# Services

Application services used by the UI (no business logic in code-behind).

## Process and extraction

| Type | Purpose |
|------|---------|
| `GuiProcessRunner` | Starts `AssetProcessor.exe`, streams stdout/stderr |
| `AssetProcessorRunner` | Builds CLI args, ties into progress and child tracking |
| `AssetProcessorRunConfig` | DTO for one UI extraction run |
| `ExtractionCoordinator` | Run/cancel orchestration and result handling |
| `ChildProcessTracker` | Kill child processes on app exit |
| `PipelineProgressTracker` | Maps backend progress to UI percent |

## Console log

| Type | Purpose |
|------|---------|
| `MainWindowLogStore` | Observable log lines for the UI |
| `ConsoleOutputCoordinator` | Buffers backend output, status line, flush to log |
| `ConsoleOutputLocalizer` | Filters and localizes backend lines |
| `ConsoleLineClassifier` | Log line kind (error, debug, progress, …) for colors |
| `ConsoleProgressParser` / `ConsoleProgressLineParser` | Progress bar and structured progress rows |
| `ExtractionRunResultAppender` | Phase 1/2 completion banners in log |
| `ExtractionIssueSummaryLoader` | Reads `issues_*.json` from AnnoAssets/logs |
| `ExtractionIssueSummaryAppender` | End-of-run issue summary in log |

## Settings and paths

| Type | Purpose |
|------|---------|
| `AppSettingsStore` / `PersistedAppSettings` | `%AppData%/AssetSplitter/settings.json` |
| `SettingsCoordinator` | Load/save/migrate settings, window size |
| `PathDisplayHelper` | Recent paths, display normalization |
| `AnnoInstallationDetector` | Steam / Epic / registry game discovery |
| `ExtractedAssetSourceLocator` | Finds `source_xml_*`, lists game languages |
| `SingleGuidAssetLookup` | Live lookup of one GUID in extracted assets |
| `GameConsoleStateStore` | Per-game log/progress snapshot when switching installs |

## Platform and theme

| Type | Purpose |
|------|---------|
| `IPlatformServices` / `PlatformServices` | Open folder, clipboard-friendly OS hooks |
| `ApplicationThemeService` | Light / dark / auto theme |
| `WindowsTitleBarTheme` | Windows chrome theming |
| `UILogger` | Debug logging for non-fatal UI errors |
