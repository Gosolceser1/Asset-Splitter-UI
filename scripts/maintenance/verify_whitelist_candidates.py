#!/usr/bin/env python3
"""Score whitelist candidates by % of refs that resolve to known asset GUIDs."""

from __future__ import annotations

import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

from _repo import report_path, repo_root

REPO = str(repo_root())
DEFAULT_OUT = r"C:\Users\vadim\Desktop\AnnoAssets\Anno1800\output_xml_anno1800"
DEFAULT_SOURCE = r"C:\Users\vadim\Desktop\AnnoAssets\Anno1800\source_xml_anno1800"
DEFAULT_WL = os.path.join(REPO, "config", "04_Comment_Whitelist", "Anno1800_Comment_Whitelist.txt")

EXCLUSIONS = {"None", "Deuteranopia", "Protanopia", "Tritanopia", "ColorMode"}
GUID_RE = re.compile(r"^\d{4,9}$")
NOISE = re.compile(
    r"(Duration|Timer|Time|Delay|Cooldown|LineID|Wwise|HitPoints|Timeout|"
    r"SubtitleGroup|TradePrice|ExecutionDelay|QuestTime|PoolCooldown|"
    r"StatusDuration|ResidentAmount|Explosion|Latency|Display|Counter|"
    r"TargetDisabled|TargetOnWater|TargetOnTerrain|HarbourAreaBuildable|"
    r"DeleteInactiveSessionUnitsAfter|FilterAssignedTradeRouteObjectOrPool|"
    r"ShipSketchPos|SketchPos|WeightPopulation|NotificationPriority|"
    r"NotificationUnread|RequiredValue|ValueConstraint|portrait_)",
    re.I,
)


def load_zero_default(path: str) -> set[str]:
    if not os.path.isfile(path):
        return set()
    return {
        n.tag
        for n in ET.parse(path).getroot().iter()
        if n.tag not in EXCLUSIONS and (n.text or "").strip() == "0"
    }


def load_whitelist(path: str) -> set[str]:
    names: set[str] = set()
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                names.add(line)
    return names


def has_comment(raw: str, tag: str, value: str) -> bool:
    return bool(re.search(re.escape(f"<{tag}>{value}</{tag}>") + r"\s*<!--", raw))


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    source = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_SOURCE
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL

    zero = load_zero_default(os.path.join(source, "properties.xml"))
    zero |= load_zero_default(os.path.join(source, "properties-toolone.xml"))
    whitelist = load_whitelist(wl_path)
    eligible = zero | whitelist

    asset_guids: set[str] = set()
    for dp, _, fs in os.walk(out_dir):
        for f in fs:
            m = re.match(r"(\d+)\s*-", f)
            if m:
                asset_guids.add(m.group(1))

    prop_refs: dict[str, Counter[str]] = defaultdict(Counter)
    prop_total: Counter[str] = Counter()

    for dp, _, fs in os.walk(out_dir):
        for f in fs:
            if not f.lower().endswith(".xml"):
                continue
            raw = open(os.path.join(dp, f), encoding="utf-8", errors="replace").read()
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue
            for elem in root.iter():
                if elem.text and "<" in elem.text:
                    continue
                if any(c.tag is ET.Comment for c in list(elem)):
                    continue
                text = (elem.text or "").strip()
                if not GUID_RE.match(text):
                    continue
                if has_comment(raw, elem.tag, text):
                    continue
                prop = elem.tag
                if prop in eligible:
                    continue
                prop_refs[prop][text] += 1
                prop_total[prop] += 1

    scored: list[tuple[float, int, int, str, list[str]]] = []
    for prop, total in prop_total.most_common():
        if NOISE.search(prop):
            continue
        refs = prop_refs[prop]
        asset_hits = sum(c for v, c in refs.items() if v in asset_guids)
        ratio = asset_hits / total if total else 0
        if total < 2 and ratio < 1:
            continue
        examples = [v for v, _ in refs.most_common(3)]
        scored.append((ratio, asset_hits, total, prop, examples))

    scored.sort(key=lambda x: (-x[0], -x[1], -x[2], x[3].casefold()))

    print("=== High-confidence asset-ref whitelist candidates (>=50% refs are known assets) ===")
    tier_a: list[str] = []
    for ratio, asset_hits, total, prop, examples in scored:
        if ratio < 0.5:
            break
        tier_a.append(prop)
        print(f"  {ratio:5.0%}  {asset_hits:4}/{total:4}  {prop}  ex={examples}")

    print(f"\n=== Medium (25-49%, count>=4) ===")
    tier_b: list[str] = []
    for ratio, asset_hits, total, prop, examples in scored:
        if ratio < 0.25 or ratio >= 0.5:
            continue
        if total < 4:
            continue
        tier_b.append(prop)
        print(f"  {ratio:5.0%}  {asset_hits:4}/{total:4}  {prop}  ex={examples}")

    print(f"\n=== UI text LineID props (name hints, any count) ===")
    ui_pat = re.compile(
        r"(Text|Prompt|Label|Button|Header|Message|Description|Title|Upgrade|Reward|"
        r"Swap|Replace|Jump|Open|Close|Reset|Build|Remove|Select|Collect|Change|"
        r"Multiple|Production|Population|Statistics|Shipyard|Cargo|Tracking|"
        r"Prologue|Monument|Participants|NoItems|Distance|Attractiveness|Quest)",
        re.I,
    )
    tier_ui: list[str] = []
    for ratio, asset_hits, total, prop, examples in scored:
        if prop in tier_a or prop in tier_b or prop in whitelist:
            continue
        if not ui_pat.search(prop):
            continue
        if NOISE.search(prop):
            continue
        tier_ui.append(prop)
        print(f"  {ratio:5.0%}  {asset_hits:4}/{total:4}  {prop}")

    suggest = sorted(set(tier_a) | set(tier_b) | set(tier_ui), key=str.casefold)
    print(f"\n=== Combined suggest list ({len(suggest)}) ===")
    for p in suggest:
        print(f"  {p}")

    report = report_path("whitelist_verified_additions.txt")
    with open(report, "w", encoding="utf-8") as fh:
        for p in suggest:
            fh.write(p + "\n")
    print(f"\nWrote {report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
