#!/usr/bin/env python3
"""One-off: add Phase 1 completion i18n keys to all Strings.*.json locale files."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LANG_DIR = ROOT / "Localization" / "Languages"
CONSOLE_DIR = ROOT / "config" / "05_Console_Messages"

LOCALES = {
    "de": "de",
    "es": "es",
    "fr": "fr",
    "it": "it",
    "pl": "pl",
    "ru": "ru",
    "ja": "ja",
    "ko": "ko",
    "zh": "zh",
    "tw": "tw",
}

CONSOLE_KEY_MAP = {
    "extractionCompleteHeader": "extractionCompleteHeader",
    "processedAssetsSummary": "processedAssetsSummary",
    "totalTimeSummary": "totalTimeSummary",
    "sourceExtractionComplete": "sourceExtractionComplete",
    "rdaExtractionCompleteReady": "rdaExtractionComplete",
    "rdaCompleteSelectLanguage": "rdaCompleteSelectLanguage",
    "rdaExtractionCompleteNonDebug": "rdaExtractionCompleteNonDebug",
    "rdaExtractionCompleteShort": "rdaExtractionCompleteShort",
}

PHASE1_COMPLETE = {
    "de": "Phase 1 abgeschlossen: Spieldateien aus RDA-Archiven extrahiert. Parameter links konfigurieren (GUID / Filter / Optionen), dann «{0}» klicken, um die Verarbeitungsphase zu starten.",
    "es": "Fase 1 completada: archivos del juego extraídos de los archivos RDA. Configure los parámetros a la izquierda (GUID / filtros / opciones), luego haga clic en «{0}» para iniciar la fase de procesamiento.",
    "fr": "Étape 1 terminée : fichiers du jeu extraits des archives RDA. Configurez les paramètres à gauche (GUID / filtres / options), puis cliquez sur « {0} » pour commencer la phase de traitement.",
    "it": "Fase 1 completata: file di gioco estratti dagli archivi RDA. Configura i parametri a sinistra (GUID / filtri / opzioni), quindi clicca «{0}» per avviare la fase di elaborazione.",
    "pl": "Faza 1 ukończona: pliki gry wyodrębnione z archiwów RDA. Skonfiguruj parametry po lewej (GUID / filtry / opcje), następnie kliknij «{0}», aby rozpocząć fazę przetwarzania.",
    "ru": "Этап 1 завершён: файлы игры извлечены из архивов RDA. Настройте параметры слева (GUID / фильтры / опции), затем нажмите «{0}», чтобы начать фазу обработки.",
    "ja": "フェーズ1完了：RDAアーカイブからゲームファイルを抽出しました。左側でパラメータ（GUID / フィルター / オプション）を設定し、「{0}」をクリックして処理フェーズを開始してください。",
    "ko": "1단계 완료: RDA 아카이브에서 게임 파일을 추출했습니다. 왼쪽에서 매개변수(GUID / 필터 / 옵션)를 설정한 다음 «{0}»을(를) 클릭하여 처리 단계를 시작하세요.",
    "zh": "阶段 1 完成：已从 RDA 存档提取游戏文件。在左侧配置参数（GUID / 筛选 / 选项），然后点击「{0}」开始处理阶段。",
    "tw": "階段 1 完成：已從 RDA 封存提取遊戲檔案。在左側設定參數（GUID / 篩選 / 選項），然後點擊「{0}」開始處理階段。",
}

RDA_READY_OVERRIDES = {
    "ru": "[RDA COMPLETE] ✓ Файлы игры извлечены. Настройте параметры в GUI и запустите снова для обработки ресурсов.",
    "ja": "[RDA完了] ✓ ゲームファイルを抽出しました。GUIでオプションを設定し、再度実行してアセット処理を行ってください。",
    "ko": "[RDA 완료] ✓ 게임 파일을 추출했습니다. GUI에서 옵션을 설정하고 다시 실행하여 에셋 처리를 진행하세요.",
    "zh": "[RDA 完成] ✓ 游戏文件已提取。在 GUI 中配置选项并再次运行以进行资源处理。",
    "tw": "[RDA 完成] ✓ 遊戲檔案已提取。在 GUI 中設定選項並再次執行以進行資源處理。",
}

PROCESSED_ZERO = {
    "de": "Nur Phase 1 — Spieldateien extrahiert; noch keine Assets verarbeitet.",
    "es": "Solo fase 1: archivos del juego extraídos; aún no se procesaron recursos.",
    "fr": "Phase 1 uniquement — fichiers du jeu extraits ; aucune ressource traitée pour l'instant.",
    "it": "Solo fase 1 — file di gioco estratti; nessun asset ancora elaborato.",
    "pl": "Tylko faza 1 — pliki gry wyodrębnione; zasoby nie zostały jeszcze przetworzone.",
    "ru": "Только этап 1 — файлы игры извлечены; ресурсы ещё не обработаны.",
    "ja": "フェーズ1のみ — ゲームファイルを抽出しました。アセットはまだ処理されていません。",
    "ko": "1단계만 완료 — 게임 파일 추출됨; 아직 에셋은 처리되지 않았습니다.",
    "zh": "仅阶段 1 — 已提取游戏文件；尚未处理资源。",
    "tw": "僅階段 1 — 已提取遊戲檔案；尚未處理資源。",
}


def main() -> None:
    for locale, code in LOCALES.items():
        path = LANG_DIR / f"Strings.{code}.json"
        data = json.loads(path.read_text(encoding="utf-8"))
        console = json.loads((CONSOLE_DIR / f"console_{code}.json").read_text(encoding="utf-8"))

        cm = data.setdefault("consoleMessages", {})
        for ui_key, console_key in CONSOLE_KEY_MAP.items():
            value = console.get(console_key)
            if value is None:
                raise KeyError(f"{console_key} missing in console_{code}.json")
            if ui_key == "extractionCompleteHeader":
                value = value.strip()
            cm[ui_key] = value

        if locale in RDA_READY_OVERRIDES:
            cm["rdaExtractionCompleteReady"] = RDA_READY_OVERRIDES[locale]

        cm["processedZeroAssetsSummary"] = PROCESSED_ZERO[locale]
        data.setdefault("statusMessages", {})["phase1Complete"] = PHASE1_COMPLETE[locale]

        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Updated {path.name}")


if __name__ == "__main__":
    main()
