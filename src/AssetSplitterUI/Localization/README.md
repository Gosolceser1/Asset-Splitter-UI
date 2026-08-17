# Localization (UI code)

C# support for loading UI strings and binding them in XAML.

| Type | Purpose |
|------|---------|
| `StringResourceManager` | Singleton; loads `Localization/Languages/Strings*.json`; `INotifyPropertyChanged` on language change |
| `LocalizeExtension` | `{loc:Localize key.subkey}` markup extension |
| `*Converter.cs` | Value converters (e.g. `LogLineKindConverter` in `Converters/`) |

## String files

Live in the repo root: **[Localization/Languages/](../../../Localization/Languages/)** — not under this folder.

This folder is **only** the runtime loader and XAML glue.

## Changing language in code

```csharp
StringResourceManager.Instance.CurrentLanguage = "Deutsch";
```

See [Localization/README.md](../../../Localization/README.md) for adding locales and the sync workflow.
