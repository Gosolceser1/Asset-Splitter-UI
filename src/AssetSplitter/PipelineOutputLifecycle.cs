namespace AssetProcessor;



/// <summary>

/// When each flag's work runs and when output paths are created. Init resolves paths only — no mkdir.

/// </summary>

/// <remarks>

/// <b>No enhancement flags + --no-default-properties (GUI: all toggles off)</b> — Phase 1 only:

/// Extracts source_xml from RDA and stops. No Phase 3 per-asset output until a processing flag is enabled.

///

/// <b>Split-only</b> — at least one processing flag on but Phases 4–8 gated off: Phase 3 writes one XML per asset.

///

/// <b>language / none</b> — fine for source-only and for any run without GUID comments (-c).

///

/// <b>-c (AssetComments)</b>

/// Phase 2 — property scan (needs properties.xml in source);

/// Phase 4 — GUID index (InheritedIndex comments);

/// Phase 5 — merge inheritance comments on newly inserted nodes when template merge is also on;

/// Phase 7 — translated GUID comments (+ split-template annotate when --split-templates).

///

/// <b>-f (AssetFix)</b>

/// Phase 3 — enqueue parent GUIDs in single-GUID mode; lazy staging for inherited assets;

/// Phase 4 — GUID index; Phase 6 — merge BaseAssetGUID chains; refresh GUID index; remove merged staging files.

///

/// <b>-t (AssetTemplates)</b>

/// Phase 7 only — template subfolders + move formatted XML when -t is on.

///

/// <b>--no-modops-wrap</b> — Phase 3/5/6 save format (raw XML vs ModOp). Mod export wraps ModOps when writing each mod folder.

///

/// <b>--split-templates</b>

/// Phase 2B — split templates.xml (needs source templates.xml);

/// Phase 7 — GUID comments on split files when also -c.

///

/// <b>--create-asset-mods</b> — Phase 8 — one Mod Loader folder per asset; wraps ModOps on export. Template grouping only when -t is also on.

///

/// <b>-g (single GUID)</b> — Phase 3 output layout + rename; disables -t and --split-templates.

///

/// <b>-d (DebugMode)</b> — verbose logging all phases; session-only in GUI.

///

/// Phase 1 — game output root + source folder (minimal or full RDA per flags);

/// Phase 2 — config bootstrap; optional dictionaries + property scan;

/// Phase 3 — asset output folder; lazy staging only when -f;

/// End — logs directory + fixer scratch cleanup.

/// </remarks>

internal static class PipelineOutputLifecycle;

