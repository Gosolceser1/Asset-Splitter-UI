# 04_Comment_Whitelist

Property **XML element names** that may receive translated GUID comments when **GUID comments** (`-c` / UI toggle) is enabled.

| File | Game |
|------|------|
| `Anno1800_Comment_Whitelist.txt` | Anno 1800 |
| `Anno117_Comment_Whitelist.txt` | Anno 117 |

## Format

One property name per line (e.g. `Product`, `Item`, `Factory7`).

## When to edit

- Add property names where inline `<!-- GUID name -->` comments help modders.
- Remove names to reduce comment noise and file size.

Only whitelisted properties are annotated; other GUID references in XML are left unchanged.

Details: [config README](../README.md).
