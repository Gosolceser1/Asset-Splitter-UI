#!/usr/bin/env python3
import os
import re
import sys
import xml.etree.ElementTree as ET

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\vadim\Desktop\AnnoAssets\Anno1800\output_xml_anno1800"

CHECK = [
    "DefaultActorVariation", "BonusNeed", "RefGuidGamepad", "ConstructionMenuIcon",
    "FilterIcon", "RequiredBuilding", "TutorialRefGUID", "BlueprintBuildingType",
    "NeedSatisfactionProduct", "ShipCommandAttack", "UpgradeText", "OwnerChangeTarget",
    "ScenarioWorkshopPackage", "SeasonFluffText", "CopyAssetValue", "ShowInAssetTree",
]


def main() -> int:
    stats = {p: {"total": 0, "commented": 0} for p in CHECK}
    comment_total = 0
    files_with_comments = 0
    files = 0

    for dirpath, _, names in os.walk(OUT):
        for name in names:
            if not name.lower().endswith(".xml"):
                continue
            files += 1
            path = os.path.join(dirpath, name)
            raw = open(path, encoding="utf-8", errors="replace").read()
            c = len(re.findall(r"<!--", raw))
            comment_total += c
            if c:
                files_with_comments += 1
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue
            for prop in CHECK:
                for elem in root.iter(prop):
                    val = (elem.text or "").strip()
                    if not re.fullmatch(r"\d{4,9}", val):
                        continue
                    stats[prop]["total"] += 1
                    snippet = f"<{prop}>{val}</{prop}>"
                    if re.search(re.escape(snippet) + r"\s*<!--", raw):
                        stats[prop]["commented"] += 1

    print(f"Files: {files:,}")
    print(f"Files with comments: {files_with_comments:,}")
    print(f"Total <!-- markers: {comment_total:,}")
    print()
    for prop in CHECK:
        s = stats[prop]
        pct = 100 * s["commented"] / s["total"] if s["total"] else 0
        print(f"  {prop:30} {s['commented']:4}/{s['total']:4}  ({pct:.0f}%)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
