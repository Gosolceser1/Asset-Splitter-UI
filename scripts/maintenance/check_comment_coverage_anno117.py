#!/usr/bin/env python3
import os, re, sys, xml.etree.ElementTree as ET

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\vadim\Desktop\AnnoAssets\Anno117\output_xml_anno117"
CHECK = [
    "AddSummaryText", "ProductCategory", "BlueprintBuildingType", "Sequence",
    "InfoPanelHeadline", "OverrideBodyText", "RefGuidGamepad", "BaseAssetGUID",
    "QuestEntry", "Profile", "SideQuestPoolGUID", "PortraitName", "MonumentHeadline",
    "InfoPanelDescription", "ExecutionPlaceFullText", "HeaderText", "BodyText",
    "CivilianShipIcon", "MilitaryShipIcon", "RoadGUID", "TutorialRefGUID",
]


def main() -> int:
    stats = {p: {"t": 0, "c": 0} for p in CHECK}
    comments = files = fc = 0
    for dp, _, fs in os.walk(OUT):
        for f in fs:
            if not f.lower().endswith(".xml"):
                continue
            files += 1
            raw = open(os.path.join(dp, f), encoding="utf-8", errors="replace").read()
            c = len(re.findall(r"<!--", raw))
            comments += c
            if c:
                fc += 1
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue
            for prop in CHECK:
                for elem in root.iter(prop):
                    val = (elem.text or "").strip()
                    if not re.fullmatch(r"-?\d+", val):
                        continue
                    stats[prop]["t"] += 1
                    snip = f"<{prop}>{val}</{prop}>"
                    if re.search(re.escape(snip) + r"\s*<!--", raw):
                        stats[prop]["c"] += 1
    print(f"Files: {files:,}  with comments: {fc:,}  markers: {comments:,}\n")
    for prop in CHECK:
        s = stats[prop]
        pct = 100 * s["c"] / s["t"] if s["t"] else 0
        print(f"  {prop:28} {s['c']:5}/{s['t']:5}  ({pct:.0f}%)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
