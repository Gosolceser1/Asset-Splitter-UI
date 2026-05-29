# 01_Templates

Lists which **asset templates** are extracted from the game database into per-asset XML files.

| File | Game |
|------|------|
| `Anno1800_Templates.txt` | Anno 1800 |
| `Anno117_Templates.txt` | Anno 117 |

## Format

- One template name per line (UTF-8), e.g. `FactoryBuilding7`, `Item`.
- Lines starting with `#` are ignored if present (convention only — verify loader behavior before relying on comments).

## When to edit

- Add a template name to extract more asset types.
- Remove names to shrink output and speed up runs.

## Updates from game

Use CLI **`--update-templates`** to refresh lists from the installed game's `templates.xml` (see [config README](../README.md)).

## Pipeline

Used in **Phase 3** (asset extraction). Assets whose `<Template>` is not listed are skipped.
