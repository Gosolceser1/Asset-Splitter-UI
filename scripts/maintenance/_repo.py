from __future__ import annotations

from pathlib import Path


def repo_root() -> Path:
    for path in Path(__file__).resolve().parents:
        if (path / "AssetSplitter.sln").is_file():
            return path
    raise RuntimeError("Could not locate repository root from maintenance script path.")


def report_path(file_name: str) -> Path:
    reports = repo_root() / "docs" / "reports"
    reports.mkdir(parents=True, exist_ok=True)
    return reports / file_name
