# Languages

JSON string tables for the **desktop UI**. Each file is a nested object (e.g. `labels.gamePath`, `consoleMessages.extractionComplete`).

## Files

| File | Locale |
|------|--------|
| `Strings.json` | English (base) |
| `Strings.de.json` | Deutsch |
| `Strings.fr.json` | Français |
| `Strings.es.json` | Español |
| `Strings.it.json` | Italiano |
| `Strings.pl.json` | Polski |
| `Strings.ru.json` | Русский |
| `Strings.zh.json` | 中文 (Simplified) |
| `Strings.ja.json` | 日本語 |
| `Strings.ko.json` | 한국어 |
| `Strings.tw.json` | 繁體中文 |

## Rules

- **Same keys** in every file — only translate values.
- **UTF-8** encoding.
- Use `{0}`, `{1}` placeholders exactly as in English (order may change per language).
- Valid JSON only (no trailing commas, no comments).

## Sections (high level)

| Section | Examples |
|---------|----------|
| `app`, `labels`, `buttons` | Window chrome |
| `consoleMessages` | UI copy for run banners and status |
| `statusMessages`, `dialogs` | Status bar and message boxes |
| `developerReport`, `issueSummary` | Developer tools / issue summary |
| `tooltips`, `descriptions` | Help text |

## Adding a new UI language

1. Copy `Strings.json` → `Strings.xx.json`.
2. Translate values.
3. Add console file `config/05_Console_Messages/console_xx.json` (copy from `console_en.json`).
4. Register the language in `StringResourceManager` (`LanguageMap`) and UI language picker.

Parent overview: [../README.md](../README.md).
