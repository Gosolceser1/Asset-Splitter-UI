# Views

Avalonia XAML views and minimal code-behind.

| File | Purpose |
|------|---------|
| `MainWindow.axaml` | Main window — paths, options, progress, developer tools, console `ListBox` |
| `MainWindow.axaml.cs` | Window events (copy log, developer report, theme hooks); delegates logic to `MainWindowViewModel` |

## Conventions

- Bindings use `{Binding ...}` on `MainWindowViewModel`.
- Localized labels: `{loc:Localize section.key}` via `LocalizeExtension`.
- Log colors: `LogLineKindConverter` on `LogLine.Kind` (see [Converters/](../Converters/)).

Avoid business logic in code-behind — add commands or services on the view model instead.
