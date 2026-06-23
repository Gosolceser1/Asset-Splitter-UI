#!/usr/bin/env python3
"""Sync config/05_Console_Messages/console_*.json with ConsoleMessages.cs defaults."""

from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONSOLE_DIR = ROOT / "config" / "05_Console_Messages"
CS_FILE = ROOT / "src" / "AssetSplitter" / "ConsoleMessages.cs"

ORPHAN_KEYS = {"readmeShortStep5"}

# User-facing keys that should be translated (not left as English) per language.
LANG_OVERRIDES: dict[str, dict[str, str]] = {
    "de": {
        "clearedExistingSourceXmlFolder": "[INFO] Vorhandener source_xml-Cache-Ordner geleert",
        "clearedExistingTemplateSplitFolder": "[INFO] Vorhandener output_templates-Ordner geleert",
        "sourceXmlFolderPrepareFailed": "[ERROR] source_xml-Ordner '{0}' konnte nicht vorbereitet werden: {1}",
        "modOutputFolderPrepareFailed": "[ERROR] Mod-Ausgabeordner '{0}' konnte nicht vorbereitet werden: {1}",
    },
    "fr": {
        "clearedExistingSourceXmlFolder": "[INFO] Dossier cache source_xml existant efface",
        "clearedExistingTemplateSplitFolder": "[INFO] Dossier output_templates existant efface",
        "sourceXmlFolderPrepareFailed": "[ERROR] Impossible de preparer le dossier source_xml '{0}' : {1}",
        "modOutputFolderPrepareFailed": "[ERROR] Impossible de preparer le dossier de sortie mod '{0}' : {1}",
    },
    "es": {
        "clearedExistingSourceXmlFolder": "[INFO] Carpeta cache source_xml existente borrada",
        "clearedExistingTemplateSplitFolder": "[INFO] Carpeta output_templates existente borrada",
        "sourceXmlFolderPrepareFailed": "[ERROR] No se pudo preparar la carpeta source_xml '{0}': {1}",
        "modOutputFolderPrepareFailed": "[ERROR] No se pudo preparar la carpeta de salida mod '{0}': {1}",
    },
    "it": {
        "clearedExistingSourceXmlFolder": "[INFO] Cartella cache source_xml esistente cancellata",
        "clearedExistingTemplateSplitFolder": "[INFO] Cartella output_templates esistente cancellata",
        "sourceXmlFolderPrepareFailed": "[ERROR] Impossibile preparare la cartella source_xml '{0}': {1}",
        "modOutputFolderPrepareFailed": "[ERROR] Impossibile preparare la cartella mod di output '{0}': {1}",
    },
    "pl": {
        "clearedExistingSourceXmlFolder": "[INFO] Wyczyszczono istniejacy folder cache source_xml",
        "clearedExistingTemplateSplitFolder": "[INFO] Wyczyszczono istniejacy folder output_templates",
        "sourceXmlFolderPrepareFailed": "[ERROR] Nie mozna przygotowac folderu source_xml '{0}': {1}",
        "modOutputFolderPrepareFailed": "[ERROR] Nie mozna przygotowac folderu wyjscia mod '{0}': {1}",
    },
    "ru": {
        "clearedExistingSourceXmlFolder": "[INFO] Очищена существующая папка кэша source_xml",
        "clearedExistingTemplateSplitFolder": "[INFO] Очищена существующая папка output_templates",
        "sourceXmlFolderPrepareFailed": "[ERROR] Не удалось подготовить папку source_xml '{0}': {1}",
        "modOutputFolderPrepareFailed": "[ERROR] Не удалось подготовить папку вывода mod '{0}': {1}",
    },
    "ja": {
        "clearedExistingSourceXmlFolder": "[INFO] 既存の source_xml キャッシュフォルダをクリアしました",
        "clearedExistingTemplateSplitFolder": "[INFO] 既存の output_templates フォルダをクリアしました",
        "sourceXmlFolderPrepareFailed": "[ERROR] source_xml フォルダ '{0}' を準備できませんでした: {1}",
        "modOutputFolderPrepareFailed": "[ERROR] mod 出力フォルダ '{0}' を準備できませんでした: {1}",
    },
    "ko": {
        "clearedExistingSourceXmlFolder": "[INFO] 기존 source_xml 캐시 폴더를 비웠습니다",
        "clearedExistingTemplateSplitFolder": "[INFO] 기존 output_templates 폴더를 비웠습니다",
        "sourceXmlFolderPrepareFailed": "[ERROR] source_xml 폴더 '{0}'을(를) 준비하지 못했습니다: {1}",
        "modOutputFolderPrepareFailed": "[ERROR] mod 출력 폴더 '{0}'을(를) 준비하지 못했습니다: {1}",
    },
    "zh": {
        "clearedExistingSourceXmlFolder": "[INFO] 已清除现有 source_xml 缓存文件夹",
        "clearedExistingTemplateSplitFolder": "[INFO] 已清除现有 output_templates 文件夹",
        "sourceXmlFolderPrepareFailed": "[ERROR] 无法准备 source_xml 文件夹 '{0}'：{1}",
        "modOutputFolderPrepareFailed": "[ERROR] 无法准备 mod 输出文件夹 '{0}'：{1}",
    },
    "tw": {
        "clearedExistingSourceXmlFolder": "[INFO] 已清除現有 source_xml 快取資料夾",
        "clearedExistingTemplateSplitFolder": "[INFO] 已清除現有 output_templates 資料夾",
        "sourceXmlFolderPrepareFailed": "[ERROR] 無法準備 source_xml 資料夾 '{0}'：{1}",
        "modOutputFolderPrepareFailed": "[ERROR] 無法準備 mod 輸出資料夾 '{0}'：{1}",
    },
}


def parse_default_messages() -> dict[str, str]:
    lines = CS_FILE.read_text(encoding="utf-8").splitlines()
    defaults: dict[str, str] = {}
    in_block = False
    for line in lines:
        if "DefaultMessages = new()" in line:
            in_block = True
            continue
        if in_block:
            if line.strip() == "};":
                break
            match = re.match(r'\s+\["([^"]+)"\]\s*=\s*"(.*)",?\s*$', line)
            if match:
                key, value = match.group(1), match.group(2)
                value = bytes(value, "utf-8").decode("unicode_escape")
                defaults[key] = value
    return defaults


def load_json(path: Path) -> dict[str, str]:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: Path, data: dict[str, str]) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def merge_master(en_path: Path, defaults: dict[str, str]) -> dict[str, str]:
    master = load_json(en_path)
    for key, value in defaults.items():
        master.setdefault(key, value)
    return master


def sync_language(path: Path, master: dict[str, str], lang: str) -> tuple[int, int]:
    data = load_json(path)
    added = 0
    removed = 0
    overrides = LANG_OVERRIDES.get(lang, {})

    for orphan in ORPHAN_KEYS:
        if orphan in data:
            del data[orphan]
            removed += 1

    for key, en_value in master.items():
        if key not in data:
            data[key] = overrides.get(key, en_value)
            added += 1

    save_json(path, data)
    return added, removed


def main() -> None:
    defaults = parse_default_messages()
    en_path = CONSOLE_DIR / "console_en.json"
    master = merge_master(en_path, defaults)
    save_json(en_path, master)

    print(f"Master (console_en.json): {len(master)} keys")

    for path in sorted(CONSOLE_DIR.glob("console_*.json")):
        if path.name == "console_en.json":
            continue
        lang = path.stem.replace("console_", "")
        added, removed = sync_language(path, master, lang)
        missing = set(master) - set(load_json(path))
        print(f"{lang}: +{added} -{removed} missing_after={len(missing)}")


if __name__ == "__main__":
    main()
