#!/usr/bin/env python3
"""Explain why Anno 117 LineId fields lack translation comments."""

from __future__ import annotations

import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

from _repo import report_path, repo_root

REPO = str(repo_root())
DEFAULT_OUT = r"C:\Users\vadim\Desktop\AnnoAssets\Anno117\output_xml_anno117"
DEFAULT_TEXTS = r"C:\Users\vadim\Desktop\AnnoAssets\Anno117\source_xml_anno117\texts_english.xml"
DEFAULT_WL = os.path.join(REPO, "config", "04_Comment_Whitelist", "Anno117_Comment_Whitelist.txt")

LINE_ID_RE = re.compile(r"^-?\d{10,20}$")

PROPS_FOCUS = [
    "AddSummaryText", "ProductCategory", "PortraitName", "OverrideBodyText",
    "InfoPanelHeadline", "InfoPanelDescription", "HeaderText", "BodyText",
    "OasisId", "Text", "AudioText", "Subtitle", "RefGuidGamepad",
]


def load_eligible(wl_path: str, props_path: str) -> set[str]:
    wl: set[str] = set()
    with open(wl_path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if line and not line.startswith("#"):
                wl.add(line)
    zero: set[str] = set()
    if os.path.isfile(props_path):
        for node in ET.parse(props_path).getroot().iter():
            if (node.text or "").strip() == "0" and node.tag != "None":
                zero.add(node.tag)
    return wl | zero


def load_translations(texts_path: str) -> dict[str, str]:
    out: dict[str, str] = {}
    if not os.path.isfile(texts_path):
        return out
    for text_node in ET.parse(texts_path).getroot().iter("Text"):
        key = None
        for child in text_node:
            if child.tag in ("LineId", "GUID"):
                key = (child.text or "").strip()
        if not key:
            continue
        value_node = text_node.find("Text")
        out[key] = (value_node.text or "").strip() if value_node is not None else ""
    return out


def classify_translation(key: str, translations: dict[str, str]) -> str:
    if key not in translations:
        return "missing_from_language_file"
    text = translations[key]
    if not text:
        return "empty_translation_in_language_file"
    if len(text.strip()) < 2:
        return "translation_too_short"
    trimmed = text.strip("-_ ")
    if not trimmed:
        return "sanitizer_would_strip_all"
    return "should_have_comment"


def has_comment(raw: str, tag: str, value: str) -> bool:
    return bool(re.search(re.escape(f"<{tag}>{value}</{tag}>") + r"\s*<!--", raw))


def main() -> int:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT
    texts_path = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_TEXTS
    wl_path = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_WL
    props_path = os.path.join(os.path.dirname(texts_path), "properties.xml")

    eligible = load_eligible(wl_path, props_path)
    translations = load_translations(texts_path)

    line_stats = Counter()
    for key, text in translations.items():
        if not LINE_ID_RE.match(key):
            continue
        if not text:
            line_stats["empty_in_lang_file"] += 1
        elif len(text.strip()) < 2:
            line_stats["short_in_lang_file"] += 1
        else:
            line_stats["usable_in_lang_file"] += 1

    print(f"Language file: {texts_path}")
    print(f"LineId keys: {sum(line_stats.values()):,}")
    for k, v in line_stats.most_common():
        print(f"  {k}: {v:,}")

    gap_reasons: Counter[str] = Counter()
    gap_examples: dict[str, list[tuple]] = defaultdict(list)
    prop_gaps: Counter[str] = Counter()
    prop_totals: Counter[str] = Counter()
    comment_markers = 0
    files = 0

    for dirpath, _, names in os.walk(out_dir):
        for name in names:
            if not name.lower().endswith(".xml"):
                continue
            files += 1
            path = os.path.join(dirpath, name)
            try:
                raw = open(path, encoding="utf-8", errors="replace").read()
            except OSError:
                continue
            comment_markers += len(re.findall(r"<!--", raw))
            try:
                root = ET.fromstring(raw)
            except ET.ParseError:
                continue

            for elem in root.iter():
                if elem.text and "<" in elem.text:
                    continue
                value = (elem.text or "").strip()
                if not LINE_ID_RE.match(value):
                    continue
                prop = elem.tag
                if prop not in PROPS_FOCUS:
                    continue
                prop_totals[prop] += 1
                if has_comment(raw, prop, value):
                    continue
                prop_gaps[prop] += 1

                if prop not in eligible:
                    reason = "property_not_eligible"
                else:
                    reason = classify_translation(value, translations)

                gap_reasons[reason] += 1
                if len(gap_examples[reason]) < 4:
                    snippet = translations.get(value, "")[:70]
                    gap_examples[reason].append((prop, value, name[:55], snippet))

    print(f"\nOutput: {files:,} files, {comment_markers:,} comment markers")
    print("\n=== Focus properties (LineId values) ===")
    for prop in PROPS_FOCUS:
        total = prop_totals[prop]
        if not total:
            continue
        gaps = prop_gaps[prop]
        commented = total - gaps
        print(f"  {prop:28} {commented:5}/{total:5} commented  ({gaps} missing)")

    print("\n=== Why LineId comments are missing ===")
    for reason, count in gap_reasons.most_common():
        print(f"  {count:5}  {reason}")
        for ex in gap_examples[reason]:
            print(f"         {ex[0]} {ex[1]}  file={ex[2]}")
            if ex[3]:
                print(f"           text={ex[3]!r}")

    report = report_path("anno117_lineid_gap_analysis_english.txt")
    with open(report, "w", encoding="utf-8") as fh:
        fh.write(f"Language: {texts_path}\n\n")
        for reason, count in gap_reasons.most_common():
            fh.write(f"{count}\t{reason}\n")
    print(f"\nWrote {report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
