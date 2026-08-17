# Localization (UI)

UI strings for **Asset Splitter UI** (Avalonia). Loaded at runtime from JSON under `Languages/`.

## Structure

| Path | Purpose |
|------|---------|
| `Languages/Strings.json` | English (source of truth for UI key shape) |
| `Languages/Strings.{lang}.json` | Translated UI (`de`, `fr`, `es`, `it`, `pl`, `ru`, `zh`, `ja`, `ko`, `tw`) |

**Code:** `src/AssetSplitterUI/Localization/StringResourceManager.cs` and XAML `{loc:Localize ...}` bindings.

## Backend vs UI

| System | Files | Used by |
|--------|-------|---------|
| UI | `Localization/Languages/Strings*.json` | Avalonia app |
| Console + mod README text | `config/05_Console_Messages/console_*.json` | `AssetProcessor.exe`, `ModReadmeWriter` |

Translations are maintained directly in `Localization/Languages/Strings*.json` and `config/05_Console_Messages/console_*.json`.

## Adding or updating translations

**UI only (manual):**

1. Edit `Strings.json` (English) or a `Strings.xx.json` file.
2. Keep keys identical across all locale files.
3. UTF-8, valid JSON (no comments).

**All sections:**

1. Edit English in `Strings.json` / `console_en.json` as needed.
2. Mirror keys in each `Strings.{lang}.json` and `console_{lang}.json` file.

## Notes

- Missing keys fall back to English, then show the key name in debug builds.
- Console language follows UI language selection where a matching `console_{lang}.json` exists.

See also [Languages/README.md](Languages/README.md).
