# 02_Processing_Rules

**Fixlists** — templates that receive full template-property merge and `BaseAssetGUID` dependency resolution (slower, more complete output).

| File | Game |
|------|------|
| `Anno1800_Fixlist.txt` | Anno 1800 |
| `Anno117_Fixlist.txt` | Anno 117 |

## Format

One template name per line (UTF-8), same naming as in `01_Templates/`.

## When to edit

- Add templates that need inherited properties merged from `templates.xml` / `properties.xml`.
- Keep the fixlist **smaller** than the template list — only templates that benefit from merge/deps.

## Pipeline

- **Phase 4** — template merge (`TemplateMergeOrchestrator`)
- **Phase 5** — dependency resolution

Override: `-x:path/to/custom_fixlist.txt`

Details: [config README](../README.md).
