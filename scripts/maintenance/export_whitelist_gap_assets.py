#!/usr/bin/env python3
"""List asset GUIDs where whitelist-missing properties have uncommented GUID refs."""

from __future__ import annotations

import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

from _repo import report_path, repo_root

REPO = str(repo_root())
DEFAULT_OUT = r"C:\Users\vadim\Desktop\AnnoAssets\Anno1800\output_xml_anno1800"
DEFAULT_SOURCE = r"C:\Users\vadim\Desktop\AnnoAssets\Anno1800\source_xml_anno1800"
DEFAULT_WL = os.path.join(
    REPO,
    "config",
    "04_Comment_Whitelist",
    "Anno1800_Comment_Whitelist.txt",
)

def load_whitelist(path: str) -> set[str]:
    names: set[str] = set()
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                names.add(line)
    return names


def load_zero_default_props(path: str) -> set[str]:
    names: set[str] = set()
    if not os.path.isfile(path):
        return names
    for node in ET.parse(path).getroot().iter():
        if node.tag not in {"None", "Deuteranopia", "Protanopia", "Tritanopia", "ColorMode"}:
            if (node.text or "").strip() == "0":
                names.add(node.tag)
    return names


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    report = sys.argv[2] if len(sys.argv) > 2 else str(report_path("whitelist_gap_assets.txt"))
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL
    source = sys.argv[4] if len(sys.argv) > 4 else DEFAULT_SOURCE

    zero = load_zero_default_props(os.path.join(source, "properties.xml"))
    whitelist = load_whitelist(wl_path)
    # PropertyScanner only scans properties.xml; flag props missing from both.
    missing_names = whitelist - zero

    hits: dict[str, list[dict[str, str]]] = defaultdict(list)

    for dirpath, _, files in os.walk(out_dir):
        for name in files:
            if not name.lower().endswith(".xml"):
                continue
            path = os.path.join(dirpath, name)
            raw = open(path, encoding="utf-8", errors="replace").read()
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue

            guid_el = root.find(".//Standard/GUID")
            asset_guid = (guid_el.text or "").strip() if guid_el is not None else ""
            tpl_el = root.find(".//Asset/Template") or root.find("./Template")
            template = (tpl_el.text or "").strip() if tpl_el is not None else ""

            for elem in root.iter():
                if elem.tag not in missing_names:
                    continue
                text = (elem.text or "").strip()
                if not text or not re.fullmatch(r"\d{5,9}", text):
                    continue
                if any(child.tag is not ET.Comment for child in list(elem)):
                    continue
                snippet = f"<{elem.tag}>{text}</{elem.tag}>"
                if re.search(re.escape(snippet) + r"\s*<!--", raw):
                    continue
                hits[elem.tag].append(
                    {
                        "asset_guid": asset_guid,
                        "ref_guid": text,
                        "file": name,
                        "path": path,
                        "template": template,
                    }
                )

    lines = [
        "Anno 1800 - assets with whitelist-missing properties (uncommented GUID refs)",
        f"Source: {out_dir}",
        f"Total hits: {sum(len(v) for v in hits.values())}",
        "=" * 80,
    ]

    for prop in sorted(hits, key=lambda p: (-len(hits[p]), p)):
        rows = hits[prop]
        lines.append("")
        lines.append(f"## {prop}  ({len(rows)} hits)")
        lines.append("-" * 80)
        seen: set[tuple[str, str, str]] = set()
        for row in rows:
            key = (row["asset_guid"], row["ref_guid"], row["file"])
            if key in seen:
                continue
            seen.add(key)
            lines.append(
                f"asset GUID {row['asset_guid']:>8}  -> ref {row['ref_guid']:>8}  template={row['template']}"
            )
            lines.append(f"  file: {row['file']}")
            lines.append(f"  path: {row['path']}")

    with open(report, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print(f"Report: {report}")
    print(f"Total hits: {sum(len(v) for v in hits.values())}")
    for prop in sorted(hits, key=lambda p: (-len(hits[p]), p)):
        print(f"\n{prop} ({len(hits[prop])}):")
        for row in hits[prop][:8]:
            print(f"  asset {row['asset_guid']} -> ref {row['ref_guid']}  |  {row['file']}")
        if len(hits[prop]) > 8:
            print(f"  ... +{len(hits[prop]) - 8} more in report file")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
