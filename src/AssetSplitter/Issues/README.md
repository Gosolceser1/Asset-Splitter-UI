# Pipeline issues

Structured tracking for **developer / debug mode** — so failures are grouped at the end of a run instead of buried in a huge log.

## Components

| Type | Role |
|------|------|
| `PipelineIssueCodes` | Stable codes (`ParentAssetNotInGuidIndex`, `MergeAssetFailed`, …) |
| `PipelineIssue` | One recorded problem (GUID, path, message, root cause, hint) |
| `PipelineIssueTracker` | Collects issues during the pipeline (`context.Issues`) |
| `PipelineIssueReporter` | Writes `AnnoAssets/logs/issues_YYYYMMDD_HHmmss_fff.json` and optional console summary |

## When issues are recorded

Examples: parent GUID not in index, merge/format/mod-package failures, RDA errors. Normal debug lines for assets named `*Failed*` or `*Error*` are **not** issues — they are UI popup assets.

## UI consumption

- End of run: `ExtractionIssueSummaryAppender` loads **this run's** report via the backend `[ISSUE_SUMMARY]` marker (falls back to newest `issues_*.json`). Zero-issue runs still write an empty report so a prior failure is never shown after SUCCESS.
- **Copy report**: `MainWindowViewModel` includes the same data plus console `[ERROR]` / `[WARN]` lines.

## Localization

- Backend summaries: `config/05_Console_Messages/console_*.json` (`issue*` keys).
- UI grouping titles: `Localization/Languages/Strings.*.json` → `issueSummary.codes.*`.

Sync workflow: keep issueSummary keys aligned across Localization/Languages/Strings*.json and config/05_Console_Messages/console_*.json.
