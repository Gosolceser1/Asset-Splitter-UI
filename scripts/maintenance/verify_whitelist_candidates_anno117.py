#!/usr/bin/env python3
"""Score Anno 117 whitelist candidates by asset-resolution rate."""

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
ASSET_GUID_RE = re.compile(r"^\d{1,9}$")
LINE_ID_RE = re.compile(r"^-?\d{10,20}$")

NOISE = re.compile(
    r"(Duration|Timer|Time|Delay|Cooldown|LineID|Wwise|HitPoints|Price|"
    r"Timeout|Counter|Budget|ResidentAmount|Explosion|Latency|Display|"
    r"StatusDuration|Interval|Threshold|MinSpeed|MaxSpeed|^Speed$|^Damage$|^Health$|"
    r"Tier|Weight|SketchPos|RequiredValue|portrait_|Scaling|Percental|Hash|"
    r"Priority|Probability|Limit|Wait|IsVariable|IsBaseAutoCreate|InheritedIndex|"
    r"UpgradeKey|ModifierKey|InPercentKey|FactorKey|RadiusKey|CapacityKey|"
    r"SpeedKey|DurationKey|DistanceKey|PercentKey|MultiplierKey|CostKey|"
    r"Position|Coordinate|^X$|^Y$|^Z$|Offset|Width|Height|StartValue|"
    r"NotificationPriority|CounterAmount|Amount$|^Value$|^Target$|"
    r"BuffScaling|PopulationLevels)",
    re.I,
)

UI_LINE = re.compile(
    r"(Text|Prompt|Label|Button|Header|Message|Description|Title|Name|Hint|"
    r"Headline|Subline|Notification|Icon|Infotip|Warning|FilterDescription|"
    r"CampaignNotification|CharacterNotification|Monument|Portrait|QuestHint|"
    r"StarterFull|TargetMap|JoiningSession|AcceptQuest|Allocating|AqueductConnection)",
    re.I,
)

ASSETISH = re.compile(
    r"(GUID|Asset|Product|Unlock|Reference|Spawn|Quest|Building|Item|Skin|"
    r"Session|Context|Notification|Audio|Trigger|Module|Factory|Ship|Pool|"
    r"Upgrade|Target|Icon|Portrait|Tech|Profile|Participant|Patron|Governor|"
    r"Emperor|Rival|Workforce|Population|Storyline|Sequence|Religion|"
    r"Diplomacy|Vehicle|Unit|Wonder|Festival|Need|Provider|Template|"
    r"LightSetup|Camera|Cursor|Matcher|Ornamental|Volcano|Monument|Fleet|"
    r"Island|Province|Settlement|DLC|Character|Person|Road|Route|Prop|"
    r"Blueprint|SideQuest|Feedback|ForceRelation|ParentQuest|Victim|BulkTrade|"
    r"SessionMood|ProductFilter|ProductCategory)",
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
    out: set[str] = set()
    with open(path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                out.add(line)
    return out


def has_comment(raw: str, tag: str, value: str) -> bool:
    return bool(re.search(re.escape(f"<{tag}>{value}</{tag}>") + r"\s*<!--", raw))


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL

    whitelist = load_whitelist(wl_path)
    zero = load_zero_default(os.path.join(DEFAULT_SOURCE, "properties.xml"))
    eligible = whitelist | zero
    missing = whitelist - zero  # whitelist-only props we track for gaps

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
                val = (elem.text or "").strip()
                if not (ASSET_GUID_RE.match(val) or LINE_ID_RE.match(val)):
                    continue
                if has_comment(raw, elem.tag, val):
                    continue
                prop = elem.tag
                if prop in eligible:
                    continue
                prop_refs[prop][val] += 1
                prop_total[prop] += 1

    asset_scored: list[tuple] = []
    line_scored: list[tuple] = []

    for prop, total in prop_total.items():
        if NOISE.search(prop):
            continue
        refs = prop_refs[prop]
        asset_hits = sum(c for v, c in refs.items() if v in asset_guids)
        line_hits = sum(c for v, c in refs.items() if LINE_ID_RE.match(v))
        asset_ratio = asset_hits / total if total else 0

        if ASSETISH.search(prop) and total >= 1:
            if asset_ratio >= 0.5 or (total >= 2 and asset_hits >= 1):
                asset_scored.append((asset_hits, total, asset_ratio, prop, list(refs.keys())[:3]))
            elif UI_LINE.search(prop) and line_hits >= 1:
                line_scored.append((line_hits, total, prop))
        elif UI_LINE.search(prop) and line_hits >= 1:
            line_scored.append((line_hits, total, prop))

    asset_scored.sort(key=lambda x: (-x[0], -x[1], x[3].casefold()))
    line_scored.sort(key=lambda x: (-x[0], -x[1], x[2].casefold()))

    suggest = sorted({p for _, _, _, p, _ in asset_scored if p not in whitelist} |
                     {p for _, _, p in line_scored if p not in whitelist and _ >= 2},
                     key=str.casefold)

    print("=== Anno 117 — missing whitelist (verified) ===")
    print(f"Current whitelist: {len(whitelist)}")
    print(f"New suggestions:   {len(suggest)}\n")

    print("--- Asset-reference properties ---")
    for ah, total, ratio, prop, ex in asset_scored:
        if prop in whitelist:
            continue
        print(f"  {ah:4}/{total:4} ({ratio:0.0%})  {prop}  ex={ex}")

    print("\n--- LineId / UI text properties (>=2 hits) ---")
    for lh, total, prop in line_scored:
        if prop in whitelist or lh < 2:
            continue
        print(f"  {lh:4}/{total:4}  {prop}")

    print("\n--- Combined add list ---")
    for p in suggest:
        print(f"  {p}")

    report = report_path("anno117_whitelist_verified_additions.txt")
    with open(report, "w", encoding="utf-8") as fh:
        for p in suggest:
            fh.write(p + "\n")
    print(f"\nWrote {report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
