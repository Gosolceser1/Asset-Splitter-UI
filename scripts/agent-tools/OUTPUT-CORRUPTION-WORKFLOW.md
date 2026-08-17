# Output / source corruption workflow

Agent brief: how Asset Splitter should recover when on-disk trees are wrong — **repair from the nearest good layer**. Wipe + full pipeline and RDA re-extract are last resorts, not the default.

## Layers (nearest → farthest)

```
mod package / flat output_xml  ←  output_xml from source_xml  ←  source_xml  ←  game RDA
output_templates               ←  templates.xml (in source_xml)
inventory / stamp              ←  rewrite hashes when files intact
```

## Mental model (Process re-run)

```
detect change → hash / compare extractable source GUIDs → fix only mismatches
```

| Step | What happens |
|---|---|
| 1. Detect | Phase 1: `source_xml` digest moved but files still well-formed → **keep on disk** (no RDA wipe) |
| 2. Compare | Output cache recomputes extractable `sourceHash` map from on-disk `assets.xml` vs inventory |
| 3. Fix | **DiffRepair** / **Repair** only changed / missing GUIDs; preserve other outputs |

**Full wipe** only for: Force full rebuild, options/schema mismatch, unrecoverable corruption, or extras under `output_xml`/mods that cannot map to a GUID.

## Decision matrix

| What is bad | Trust upstream | Action |
|---|---|---|
| One mod file / mod folder | `output_xml` OK | **Repair** that GUID’s mod only (`RepairPackageGuids` / presence MissingMod) |
| One/few `output_xml` assets | `source_xml` OK | Re-extract those GUIDs from `source_xml` (**Repair** / **DiffRepair**); recreate mods |
| `output_templates` file | `templates.xml` / split source OK | **Repair** + resplit templates only (empty `RepairOnlyGuids`, skip extract) |
| `source_xml` corrupt/incomplete (unreadable, empty, not well-formed, missing required files) | Game RDAs | Phase 1 RDA re-extract, then **DiffRepair** or **Full** as needed |
| `source_xml` edited/truncated but still valid | — | Keep on disk; **DiffRepair** changed / removed GUIDs only |
| Inventory missing `sourceHash` / schema drift | — | **Backfill** when source tree + outputs intact (**Complete**); if source moved or presence fails → **DiffRepair** blanks — **not** wipe+**Full** |
| Force full rebuild checkbox / `--force-full-rebuild` | — | Wipe outputs + full pipeline (escape hatch) |

## Principle

**Repair from the nearest good layer.** Touch RDAs only when `source_xml` is untrustworthy or missing.

| Symptom | Do **not** | Do |
|---|---|---|
| Edited one flat XML / mod | Full wipe | Presence **Repair** for that GUID |
| Edited / truncated `source_xml` asset still parseable | RDA wipe + Full | Keep source; **DiffRepair** extractable hash deltas |
| Inventory rows lack `sourceHash` | Full wipe “missing hashes” | Backfill from `assets.xml` when intact; if source digest moved, DiffRepair blank-hash GUIDs |
| Empty / non-`<` / `XmlException` on required source files | Keep corrupt source | Treat as corrupt → Phase 1 RDA |
| Extra orphan under `output_xml` / mods root | Silent Complete | **Full** (cannot map extras to a GUID) |
| Options / output schema / force | Surgical repair | **Full** (intentional) |

## Content-changed vs corrupt

| Class | Meaning | Source_xml policy | Output policy |
|---|---|---|---|
| **Content-changed** | Still parseable; hash/tree digest moved (user edit or tool rewrite) | Keep on disk; DiffRepair/Complete | Presence **ContentChanged** → Repair that GUID |
| **Corrupt / incomplete** | Missing required file, zero-byte, does not start with `<`, or required XML fails load | Do **not** keep; RDA re-extract | Presence **Missing** / **Incomplete** → Repair that GUID |

## Run modes (output cache)

| Mode | Meaning |
|---|---|
| **Complete** | Outputs OK; may refresh stamp/inventory (e.g. hash backfill) |
| **Repair** | Surgical GUID / templates-only rebuild; preserve other outputs |
| **DiffRepair** | Game update or `source_xml` extractable-hash change; rebuild `RebuildGuids` only |
| **Full** | Wipe eligible outputs and rebuild everything |

## Historical bug (fixed)

Older builds could log:

1. `[SOURCE CACHE] source_xml content changed — keeping on-disk files…`
2. `[OUTPUT CACHE] Full rebuild (inventory missing source hashes).`

**(1)** is still correct. **(2) was wrong** and is gone: missing inventory `sourceHash` now **backfills** or **DiffRepair**s — never Full-wipe solely for blank hashes.

### Expected log after delete/edit in `source_xml/assets.xml`

```
[SOURCE CACHE] source_xml content changed — keeping on-disk files (build: …).
[OUTPUT CACHE] Source XML changed — diff repair +0 ~N -M (rebuild K).
  GUID 12345 — changed
  …
```

Or, if inventory lacked `sourceHash` but outputs were intact and source digest did **not** need a GUID rebuild:

```
[OUTPUT CACHE] Inventory missing source hashes — backfilling from source_xml (no full wipe).
```

Or blank hashes + dirty source / presence:

```
[OUTPUT CACHE] Inventory missing source hashes — DiffRepair after backfill check (added …, changed …, removed …, rebuild …).
```

Phase 2 / 3B still run for Repair/DiffRepair when comments/deps/templates need refresh — that is not a Full wipe of every asset file.

## Escape hatches

- UI **Force full rebuild** / CLI `--force-full-rebuild`
- Source-only run (always refreshes `source_xml` from RDA/copy)
- Delete stamp + inventory under the game output root (forces Full next run)

## Code map

| Concern | Types |
|---|---|
| Source reuse / keep / RDA | `SourceXmlCache`, `Phase1SourceAcquisition` |
| Complete / Repair / DiffRepair / Full | `OutputRunCache.Evaluate`, `PipelineOrchestrator.ApplyOutputCacheDecision` |
| Per-GUID source digests | `AssetSourceHasher`, inventory `sourceHash` |
| Presence Incomplete vs ContentChanged | `OutputRunCache.ClassifyOutputArtifact` |

## Agent checklist

1. Classify the bad layer (mod / output_xml / templates / source_xml / inventory).
2. Prefer the matrix action over “delete everything and re-extract.”
3. RDA only when required `source_xml` files fail integrity or are missing.
4. Missing `sourceHash` → backfill/DiffRepair, not Full, if outputs are recoverable.
5. Edited `source_xml` → DiffRepair changed GUIDs; never Full solely for that.
6. Force Full only when the user asked or extras cannot be mapped to GUIDs.
