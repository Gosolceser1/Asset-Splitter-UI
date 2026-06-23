#!/usr/bin/env python3
"""Audit Anno 117 comment whitelist gaps."""

from __future__ import annotations

import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

from _repo import report_path, repo_root

REPO = str(repo_root())
DEFAULT_OUT = r"C:\Users\vadim\Desktop\AnnoAssets\Anno117\output_xml_anno117"
DEFAULT_SOURCE = r"C:\Users\vadim\Desktop\AnnoAssets\Anno117\source_xml_anno117"
DEFAULT_WL = os.path.join(REPO, "config", "04_Comment_Whitelist", "Anno117_Comment_Whitelist.txt")

EXCLUSIONS = {"None", "Deuteranopia", "Protanopia", "Tritanopia", "ColorMode"}
# Asset GUID refs in output (positive, typically 1-9 digits)
ASSET_GUID_RE = re.compile(r"^\d{1,9}$")
# Anno 117 text LineIds (signed 64-bit strings in XML)
LINE_ID_RE = re.compile(r"^-?\d{10,20}$")

NOISE_NAME = re.compile(
    r"(Duration|Timer|Time|Delay|Cooldown|LineID|Wwise|Amount|HitPoints|Price|"
    r"Timeout|Counter|Budget|ResidentAmount|Explosion|Latency|Display|"
    r"StatusDuration|Interval|Threshold|MinSpeed|MaxSpeed|Speed|Damage|Health|"
    r"Tier|Weight|SketchPos|RequiredValue|portrait_|MaintenanceUpgradeKey|"
    r"UpgradeKey|ModifierKey|InPercentKey|FactorKey|RadiusKey|CapacityKey|"
    r"SpeedKey|DurationKey|DistanceKey|PercentKey|MultiplierKey|CostKey|"
    r"Position|Coordinate|^X$|^Y$|^Z$|Offset|Width|Height|MinPopulation|MaxPopulation)",
    re.I,
)

ASSETISH = re.compile(
    r"(GUID|Asset|Product|Ingredient|Unlock|Reference|Spawn|Quest|Building|ItemLink|"
    r"Skin|Session|Context|Notification|Audio|Trigger|Module|Factory|Ship|Pool|"
    r"Upgrade|Target|Icon|Portrait|Matcher|Fertility|Reward|Buff|Cargo|Tech|"
    r"Profile|Participant|Patron|Governor|Emperor|Rival|Workforce|Population|"
    r"Storyline|Sequence|Religion|Diplomacy|Vehicle|Unit|Wonder|Festival|"
    r"Need|Provider|Unlock|Template|LightSetup|Camera|Cursor|Matcher|Ornamental|"
    r"Volcano|Monument|Fleet|Island|Province|Settlement|DLC|Character|Person)",
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
    keys: set[str] = set()
    if not os.path.isfile(texts_path):
        return keys
    root = ET.parse(texts_path).getroot()
    for text_node in root.iter("Text"):
        key = None
        for child in text_node:
            if child.tag in ("LineId", "GUID"):
                key = (child.text or "").strip()
        if not key:
            continue
        value_node = text_node.find("Text")
        if value_node is not None:
            val = (value_node.text or "").strip()
            if len(val) >= 2:
                keys.add(key)
    return keys


def is_leaf(elem: ET.Element) -> bool:
    if elem.text and "<" in elem.text:
        return False
    return not any(c.tag is not ET.Comment for c in list(elem))


def has_comment_after(raw: str, tag: str, value: str) -> bool:
    snippet = f"<{tag}>{value}</{tag}>"
    return bool(re.search(re.escape(snippet) + r"\s*<!--", raw))


def is_ref_value(text: str) -> bool:
    return bool(ASSET_GUID_RE.match(text) or LINE_ID_RE.match(text))


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    source = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_SOURCE
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL

    props_main = os.path.join(source, "properties.xml")
    texts = os.path.join(source, "texts_english.xml")

    zero_main = load_zero_default_props(props_main)
    whitelist = load_whitelist(wl_path)
    eligible = zero_main | whitelist
    translations = load_translations(texts)

    print("=== Anno 117 Config ===")
    print(f"properties.xml zero-default props:     {len(zero_main):,}")
    print(f"whitelist entries:                     {len(whitelist):,}")
    print(f"eligible union:                        {len(eligible):,}")
    print(f"translatable keys (texts_english):     {len(translations):,}")

    missing_not_eligible: Counter[str] = Counter()
    missing_eligible_no_comment: Counter[str] = Counter()
    missing_translatable_asset: Counter[str] = Counter()
    missing_translatable_lineid: Counter[str] = Counter()
    examples: dict[str, list[tuple[str, str, str]]] = defaultdict(list)

    asset_guids: set[str] = set()
    files = 0
    comment_markers = 0

    for dirpath, _, names in os.walk(out_dir):
        for name in names:
            if not name.lower().endswith(".xml"):
                continue
            files += 1
            m = re.match(r"(\d+)\s*-", name)
            if m:
                asset_guids.add(m.group(1))

            path = os.path.join(dirpath, name)
            raw = open(path, encoding="utf-8", errors="replace").read()
            comment_markers += len(re.findall(r"<!--", raw))
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
                if not is_ref_value(text):
                    continue
                if has_comment_after(raw, elem.tag, text):
                    continue

                prop = elem.tag
                is_asset_ref = text in asset_guids or (
                    ASSET_GUID_RE.match(text) and text in translations
                )
                is_line_ref = LINE_ID_RE.match(text) and text in translations

                if prop in eligible:
                    missing_eligible_no_comment[prop] += 1
                    continue

                missing_not_eligible[prop] += 1
                if is_asset_ref:
                    missing_translatable_asset[prop] += 1
                    if len(examples[prop]) < 3:
                        examples[prop].append((asset_guid, text, name))
                elif is_line_ref:
                    missing_translatable_lineid[prop] += 1
                    if len(examples[prop]) < 3:
                        examples[prop].append((asset_guid, text, name))

    print(f"\n=== Output scan ({files:,} files, {comment_markers:,} comment markers) ===")

    def rank_candidates(counter: Counter[str]) -> list[tuple[int, str]]:
        items: list[tuple[int, str, int, float]] = []
        for prop, count in counter.most_common():
            if NOISE_NAME.search(prop):
                continue
            if not ASSETISH.search(prop):
                continue
            total = missing_not_eligible[prop]
            asset_hits = sum(
                1 for _, ref, _ in examples.get(prop, [])
                if ref in asset_guids
            )
            ratio = asset_hits / min(3, total) if total else 0
            items.append((count, prop, total, ratio))
        items.sort(key=lambda x: (-x[0], -x[3], -x[2], x[1].casefold()))
        return [(c, p) for c, p, _, _ in items]

    print("\n=== WHITELIST CANDIDATES — asset GUID refs (translatable, not eligible) ===")
    asset_cands = rank_candidates(missing_translatable_asset)
    print(f"Found {len(asset_cands)} property names\n")
    suggest: set[str] = set()
    for count, prop in asset_cands[:40]:
        total = missing_not_eligible[prop]
        print(f"  {count:5} translatable / {total:5} total  {prop}")
        for ag, ref, fn in examples[prop]:
            print(f"         asset {ag} -> ref {ref}  ({fn[:60]})")
        if prop not in whitelist:
            suggest.add(prop)

    print("\n=== WHITELIST CANDIDATES — LineId text refs (translatable, not eligible) ===")
    line_cands = rank_candidates(missing_translatable_lineid)
    print(f"Found {len(line_cands)} property names (top 25)\n")
    for count, prop in line_cands[:25]:
        total = missing_not_eligible[prop]
        print(f"  {count:5} translatable / {total:5} total  {prop}")
        for ag, ref, fn in examples[prop][:2]:
            print(f"         asset {ag} -> LineId {ref[:22]}...  ({fn[:50]})")
        if prop not in whitelist and count >= 2:
            suggest.add(prop)

    print(f"\n=== Suggested additions ({len(suggest)}) ===")
    for p in sorted(suggest, key=str.casefold):
        print(f"  {p}")

    print("\n=== Eligible but still uncommented (top 20) ===")
    for prop, count in missing_eligible_no_comment.most_common(20):
        print(f"  {count:6}  {prop}")

    print("\n=== Remaining non-eligible noise (top 20) ===")
    for prop, count in missing_not_eligible.most_common(20):
        print(f"  {count:6}  {prop}")

    report = report_path("anno117_whitelist_audit_report.txt")
    with open(report, "w", encoding="utf-8") as fh:
        fh.write("Suggested Anno 117 whitelist additions\n")
        fh.write("=" * 40 + "\n")
        for p in sorted(suggest, key=str.casefold):
            fh.write(p + "\n")
    print(f"\nWrote {report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
