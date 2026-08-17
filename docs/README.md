# Documentation

> **Status:** current  
> **Updated:** 2026-08-16  
> **Owner:** maintainers

Index for **Asset Splitter UI** documentation. Use the status table below to see what each doc is for and when it was last updated.

## Start here

| Audience | Document |
|----------|----------|
| Users | [../README.md](../README.md) — download, quick start, options, output layout |
| Mod authors (generated) | `MODDING-GUIDE.md` inside each `output_xml_*_mods` export (created by the tool, not stored here) |
| Developers | [guide/architecture.md](guide/architecture.md) — solution layout and data flow |
| Legal / attribution | [legal/compliance.md](legal/compliance.md), [legal/attribution.md](legal/attribution.md), [../CREDITS.md](../CREDITS.md) |
| Doc edits | [CHANGELOG.md](CHANGELOG.md) — documentation-only history (product releases: [../CHANGELOG.md](../CHANGELOG.md)) |

## Status table

| Document | Purpose | Status | Updated |
|----------|---------|--------|---------|
| [guide/architecture.md](guide/architecture.md) | Developer walkthrough of UI, pipeline, RDA | current | 2026-08-16 |
| [legal/compliance.md](legal/compliance.md) | Anno community / Ubisoft compliance notes | current | 2026-08-16 |
| [legal/attribution.md](legal/attribution.md) | ModTool decompilation lineage | current | 2026-08-16 |
| [research/rda/dlc01-prophecies-of-ash.md](research/rda/dlc01-prophecies-of-ash.md) | Anno 117 DLC01 archive research | research | 2026-04 |
| [research/rda/dlc01-triggers.md](research/rda/dlc01-triggers.md) | DLC01 campaign / quest triggers | research | 2026-04 |
| [archive/gauntlet-2026-08-16.md](archive/gauntlet-2026-08-16.md) | Quality gauntlet snapshot (not live status) | archive | 2026-08-16 |
| [reports/README.md](reports/README.md) | Generated maintenance report folder | current | 2026-08-16 |
| [CHANGELOG.md](CHANGELOG.md) | Documentation change log | current | 2026-08-16 |

Internal-only notes (gitignored): `archive/chat-handoff.md`, `archive/refactoring-log.md`.

## Related project docs (outside this folder)

| Document | Description |
|----------|-------------|
| [../src/README.md](../src/README.md) | Solution projects and entry points |
| [../scripts/README.md](../scripts/README.md) | Build, extract, and release PowerShell scripts |
| [../config/README.md](../config/README.md) | Templates, fixlists, settings, whitelists |
| [../config/05_Console_Messages/README.md](../config/05_Console_Messages/README.md) | Backend console + generated mod README strings |
| [../Localization/README.md](../Localization/README.md) | UI `Strings*.json` bundles |
| [../CHANGELOG.md](../CHANGELOG.md) | Product release notes |

## Folder layout

```text
docs/
  README.md                 ← you are here
  CHANGELOG.md              ← documentation edit history
  guide/                    ← how the software works
  legal/                    ← compliance and attribution
  research/rda/             ← game-archive research notes
  archive/                  ← dated snapshots (not day-to-day reading)
  reports/                  ← generated audits (mostly gitignored)
```

## Updating docs

1. Edit the markdown file.
2. Bump the `Updated:` date in that file’s header.
3. Update the **Status table** above if the purpose or date changed.
4. Add a short entry to [CHANGELOG.md](CHANGELOG.md).
5. Fix any inbound links if you rename or move a file.

Header convention (after the title):

```markdown
> **Status:** current
> **Updated:** YYYY-MM-DD
> **Owner:** maintainers
```

Allowed **Status** values: `current` | `research` | `archive`.
