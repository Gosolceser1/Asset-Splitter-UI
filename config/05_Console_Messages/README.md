# Console messages

User-editable JSON for **AssetProcessor.exe** console output and **generated mod documentation**.

**Languages bundled:** EN, DE, ES, FR, IT, JA, KO, PL, RU, TW, ZH (`console_{lang}.json`).

## Two uses of these files

| Key prefix | Used for |
|------------|----------|
| `extracting*`, `phase*`, `issue*`, `merge*`, … | Live console log during extraction |
| `readme*`, `readmeGuide*`, `readmeShort*`, `readmeSummary*` | Text written by **`ModReadmeWriter`** into export folders (`MODDING-GUIDE.md`, per-mod `README.md`, export root `README.md`) |

English template: **`console_en.json`**. Other languages fall back to English for missing keys.

## How language is chosen

- **GUI run:** follows UI language → maps to `console_{lang}.json` when present.
- **CLI run:** `ConsoleMessages.SetLanguage(...)` from the language argument.

## Editing

1. Change **`console_en.json`** first (new keys or wording).
2. Mirror keys in all other `console_*.json` files.
3. For mod README keys (`readme*`), translate in all `console_*.json` files you ship — there is no automatic sync for the full readme set yet.

**Placeholders:** `{0}`, `{1}` — preserved in translations.

## Adding a language

1. Copy `console_en.json` → `console_{code}.json`.
2. Translate values only; keep keys identical.
3. Add UI strings in `Localization/Languages/Strings.{code}.json` separately.

## Issue + developer strings (synced)

Keys starting with `issue` should stay aligned with UI `issueSummary` in `Localization/Languages/Strings*.json`.

## Related

- [../README.md](../README.md) — full config layout
- [../../src/AssetSplitter/ModReadmeWriter.cs](../../src/AssetSplitter/ModReadmeWriter.cs) — builds mod README files from `readme*` keys
