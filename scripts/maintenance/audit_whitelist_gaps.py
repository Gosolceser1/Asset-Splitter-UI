#!/usr/bin/env python3
"""Find Anno 1800 comment whitelist gaps: translatable GUIDs still uncommented."""

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

# Not asset GUID references — exclude from whitelist suggestions
NOISE_NAME = re.compile(
    r"(Duration|Timer|Time|Delay|Cooldown|LineID|Wwise|Amount|HitPoints|Price|"
    r"Timeout|Counter|Budget|ResidentAmount|Explosion|Latency|Display|SubtitleGroup|"
    r"TradePrice|ExecutionDelay|QuestTime|PoolCooldown|StatusDuration|Pause|Interval|"
    r"Rate|Estimated|BorderColor|PopupHeader|ModeTarget|Starters|Objectives|"
    r"ShopProduct|NotificationTextGroupFemale|AttackerSpawn|QuestGiverIdleMessage|"
    r"TargetDisabled|TargetOnWater|TargetOnTerrain|HarbourAreaBuildable|"
    r"DeleteInactiveSessionUnitsAfter|FilterAssignedTradeRouteObjectOrPool)",
    re.I,
)

ASSETISH = re.compile(
    r"(GUID|Asset|Product|Ingredient|Unlock|Reference|Spawn|Quest|Building|ItemLink|"
    r"Skin|Decal|Picking|Session|Context|Subtitle|Notification|Audio|Trigger|Module|"
    r"Factory|Harbour|Ship|Pool|Upgrade|Target|Visibility|Blueprint|Required|"
    r"Icon|Portrait|Matcher|Fertility|Reward|Buff|Cargo|Projectile|Train|"
    r"Transporter|Warehouse|Ornament|Population|Matcher|Matcher)",
    re.I,
)


def load_zero_default_props(path: str) -> set[str]:
    names: set[str] = set()
    if not os.path.isfile(path):
        return names
    for node in ET.parse(path).getroot().iter():
        if node.tag not in EXCLUSIONS and (node.text or "").strip() == "0":
            names.add(node.tag)
    return names


def load_whitelist(path: str) -> set[str]:
    names: set[str] = set()
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                names.add(line)
    return names


def load_translations(texts_path: str) -> set[str]:
    """Return GUID/LineId keys that have usable translated text (len >= 2)."""
    keys: set[str] = set()
    if not os.path.isfile(texts_path):
        return keys
    root = ET.parse(texts_path).getroot()
    for text_node in root.iter("Text"):
        guid = None
        for child in text_node:
            if child.tag == "GUID":
                guid = (child.text or "").strip()
            elif child.tag == "LineId":
                guid = (child.text or "").strip()
        if not guid:
            continue
        value_node = text_node.find("Text")
        if value_node is not None:
            val = (value_node.text or "").strip()
            if len(val) >= 2:
                keys.add(guid)
    return keys


def is_leaf(elem: ET.Element) -> bool:
    if elem.text and "<" in elem.text:
        return False
    return not any(c.tag is not ET.Comment for c in list(elem))


def has_comment_after(raw: str, tag: str, value: str) -> bool:
    snippet = f"<{tag}>{value}</{tag}>"
    return bool(re.search(re.escape(snippet) + r"\s*<!--", raw))


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    source = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_SOURCE
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL

    props_main = os.path.join(source, "properties.xml")
    props_tool = os.path.join(source, "properties-toolone.xml")
    texts = os.path.join(source, "texts_english.xml")

    zero_main = load_zero_default_props(props_main)
    zero_tool = load_zero_default_props(props_tool)
    zero_tool_only = zero_tool - zero_main
    whitelist = load_whitelist(wl_path)
    eligible = zero_main | zero_tool | whitelist
    translations = load_translations(texts)

    print("=== Config ===")
    print(f"properties.xml zero-default props:     {len(zero_main):,}")
    print(f"properties-toolone.xml zero-default:   {len(zero_tool):,}")
    print(f"toolone-only (not in main scan today):  {len(zero_tool_only):,}")
    print(f"whitelist entries:                     {len(whitelist):,}")
    print(f"eligible union:                        {len(eligible):,}")
    print(f"translatable GUID keys (texts_english): {len(translations):,}")

    # --- Scan output ---
    missing_not_eligible: Counter[str] = Counter()
    missing_eligible_no_comment: Counter[str] = Counter()
    missing_translatable: Counter[str] = Counter()  # has translation, not eligible
    examples: dict[str, list[tuple[str, str, str]]] = defaultdict(list)

    files = 0
    for dirpath, _, names in os.walk(out_dir):
        for name in names:
            if not name.lower().endswith(".xml"):
                continue
            files += 1
            path = os.path.join(dirpath, name)
            raw = open(path, encoding="utf-8", errors="replace").read()
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue

            asset_guid = ""
            g = root.find(".//Standard/GUID")
            if g is not None and g.text:
                asset_guid = g.text.strip()

            for elem in root.iter():
                if not is_leaf(elem):
                    continue
                text = (elem.text or "").strip()
                if not GUID_RE.match(text):
                    continue
                if has_comment_after(raw, elem.tag, text):
                    continue

                prop = elem.tag
                if prop in eligible:
                    missing_eligible_no_comment[prop] += 1
                    continue

                missing_not_eligible[prop] += 1
                if text in translations:
                    missing_translatable[prop] += 1
                    if len(examples[prop]) < 3:
                        examples[prop].append((asset_guid, text, name))

    print(f"\n=== Output scan ({files:,} files) ===")

    # Filter translatable gaps to asset-ish, non-noise
    candidates: list[tuple[int, str, int, int]] = []
    for prop, trans_count in missing_translatable.most_common():
        if NOISE_NAME.search(prop):
            continue
        if not ASSETISH.search(prop) and prop not in zero_tool_only:
            continue
        total = missing_not_eligible[prop]
        if trans_count == 0:
            continue
        candidates.append((trans_count, prop, total, trans_count))

    candidates.sort(reverse=True)

    print(f"\n=== WHITELIST CANDIDATES (translatable, not eligible, asset-ish) ===")
    print(f"Found {len(candidates)} property names\n")
    suggest_add: list[str] = []
    for trans_count, prop, total, _ in candidates:
        in_tool = prop in zero_tool_only
        in_wl = prop in whitelist
        print(f"  {trans_count:5} translatable / {total:5} total  {prop}"
              f"  (toolone-only zero={in_tool}, whitelist={in_wl})")
        for ag, ref, fn in examples[prop]:
            print(f"         asset {ag} -> ref {ref}  ({fn[:65]})")
        if not in_wl and trans_count >= 1:
            suggest_add.append(prop)

    print(f"\n=== Suggested additions ({len(suggest_add)}) ===")
    for p in sorted(suggest_add, key=str.casefold):
        print(f"  {p}")

    print(f"\n=== toolone-only zero-default NOT in eligible scan (PropertyScanner gap) ===")
    toolone_gaps = sorted(
        (zero_tool_only - whitelist),
        key=str.casefold,
    )
    assetish_tool = [p for p in toolone_gaps if ASSETISH.search(p) and not NOISE_NAME.search(p)]
    for p in assetish_tool[:40]:
        print(f"  {p}")
    if len(assetish_tool) > 40:
        print(f"  ... +{len(assetish_tool) - 40} more")

    print(f"\n=== Eligible but still uncommented (translation/sanitizer miss, top 20) ===")
    for prop, count in missing_eligible_no_comment.most_common(20):
        print(f"  {count:6}  {prop}")

    print(f"\n=== Remaining non-eligible (all, top 25 by count — includes noise) ===")
    for prop, count in missing_not_eligible.most_common(25):
        print(f"  {count:6}  {prop}")

    report = report_path("whitelist_audit_report.txt")
    with open(report, "w", encoding="utf-8") as fh:
        fh.write("Suggested whitelist additions\n")
        fh.write("=" * 40 + "\n")
        for p in sorted(set(suggest_add) | set(assetish_tool), key=str.casefold):
            fh.write(p + "\n")
    print(f"\nWrote {report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
