#!/usr/bin/env python3
"""Sync Localization/Languages/Strings.*.json with Strings.json structure and apply locale patches."""

from __future__ import annotations

import json
from copy import deepcopy
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
LANG_DIR = ROOT / "Localization" / "Languages"
CONSOLE_DIR = ROOT / "config" / "05_Console_Messages"

LOCALES = ("de", "es", "fr", "it", "pl", "ru", "ja", "ko", "zh", "tw")

# Keys removed from English — drop from locale files.
ORPHAN_TOOLTIP_KEYS = {
    "comments",
    "createAssetMods",
    "debugMode",
    "dependencies",
    "includeDefaultProperties",
    "language",
    "modOpsWrap",
    "noDefaultProperties",
    "splitTemplates",
    "templates",
}


def deep_merge(base: dict[str, Any], overlay: dict[str, Any]) -> dict[str, Any]:
    result = deepcopy(base)
    for key, value in overlay.items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = deep_merge(result[key], value)
        else:
            result[key] = value
    return result


def prune_orphans(data: dict[str, Any]) -> None:
    tooltips = data.get("tooltips")
    if isinstance(tooltips, dict):
        for key in ORPHAN_TOOLTIP_KEYS:
            tooltips.pop(key, None)


def flatten_keys(d: dict[str, Any], prefix: str = "") -> set[str]:
    out: set[str] = set()
    for k, v in d.items():
        path = f"{prefix}.{k}" if prefix else k
        if isinstance(v, dict):
            out |= flatten_keys(v, path)
        else:
            out.add(path)
    return out


# Per-locale value patches (only keys that changed or were missing).
PATCHES: dict[str, dict[str, Any]] = {
    "de": {
        "labels": {
            "gameDirectory": "Spiel:",
            "outputDirectory": "Ausgabe:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "Noch keine",
            "detectedInstallations": "Install:",
        },
        "placeholders": {"singleGuid": "Alle Assets"},
        "descriptions": {
            "firstRunHint": "Spielordner wählen (Auto-Erkennung oder Durchsuchen), Ausgabeordner setzen, dann „Spieldateien extrahieren“. Verarbeitungsoptionen sind nach der RDA-Extraktion verfügbar.",
            "firstRunStep1": "Auto-Erkennung oder Durchsuchen",
            "firstRunStep2": "Ausgabeordner wählen",
            "firstRunStep3": "Spieldateien extrahieren",
            "language": "Nur GUID-Kommentare",
            "singleGuid": "Optional — ein Asset per GUID",
        },
        "checkboxes": {
            "comments": "GUID-Kommentare",
            "dependencies": "Eltern auflösen",
            "includeDefaultProperties": "Standardeigenschaften",
            "modOpsWrap": "ModOps-XML",
            "templates": "Vorlagenordner",
            "splitTemplates": "Vorlagendateien teilen",
            "createAssetMods": "Asset-Mod-Ordner",
            "debugMode": "Ausführliches Protokoll",
        },
        "dialogs": {
            "selectLanguageForComments": "GUID-Kommentare ist aktiv — wähle eine Spielsprache (texts_*.xml), bevor du verarbeitest.",
            "noLanguagesForComments": "GUID-Kommentare ist aktiv, aber es wurden keine texts_*.xml-Dateien gefunden.\n\nFühre zuerst die Extraktion aus, um Sprachdateien aus den RDA-Archiven zu erhalten.",
        },
        "statusMessages": {
            "invalidGameDirectory": "Kein gültiger Anno-Spielordner — wähle den Ordner mit maindata und .rda-Dateien (z. B. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "Mehrere Anno-Spiele in diesem Ordner — wähle eines unten aus",
            "languageSetupBannerTitle": "GUID-Kommentare benötigen eine Spielsprache",
            "languageSetupBannerDetail": "Wähle links eine texts_*.xml-Datei oder deaktiviere GUID-Kommentare, um ohne Übersetzungen zu verarbeiten.",
        },
        "tooltips": {
            "extract": "Schritt 3 beim ersten Start: Rohe Spieldateien aus RDA-Archiven extrahieren. Später: Assets mit den Optionen links verarbeiten.",
            "processingOptionsWhenLocked": "Gesperrt, bis Spieldateien extrahiert wurden.\n\nSpiel- und Ausgabepfad setzen, dann Extrahieren. Diese Optionen werden freigeschaltet, sobald source_xml auf der Festplatte liegt.",
            "processingOptionsWhenReady": "Lege fest, wie extrahierte Assets verarbeitet werden, und klicke dann auf „Assets verarbeiten“.",
            "singleGuid": "Meist leer lassen — dann werden alle Assets extrahiert.\n\nFür ein einzelnes Asset die numerische GUID eingeben (z. B. 1010017). Bei Treffer in assets.xml erscheint eine Vorschau.\n\nEinzel-GUID-Modus deaktiviert Vorlagenordner und geteilte Vorlagen. Eltern auflösen kann weiterhin Elterndaten zusammenführen.",
            "languageWhenOff": "Spielsprache ist gesperrt.\n\nGUID-Kommentare aktivieren, um eine texts_*.xml-Datei zu wählen. Alle anderen Optionen funktionieren ohne Spielsprache.",
            "languageWhenOn": "texts_*.xml für übersetzte GUID-Kommentare wählen.\n\nIst die Liste leer, zuerst extrahieren.",
            "commentsWhenOff": "Deaktiviert — keine übersetzten Namenskommentare in der XML.\n\nDie Spielsprache bleibt gesperrt.",
            "commentsWhenOn": "Aktiviert — fügt übersetzte Namen neben jeder GUID hinzu.\n\nSchaltet auch die Spielsprache oben frei.",
            "dependenciesWhenOff": "Deaktiviert — BaseAssetGUID-Elterndaten werden nicht zusammengeführt.",
            "dependenciesWhenOn": "Aktiviert — führt vererbte BaseAssetGUID-Ketten in jedes Asset ein.",
            "includeDefaultPropertiesWhenOff": "Deaktiviert — fehlende Vorlagenfelder werden nicht aus properties.xml ergänzt.",
            "includeDefaultPropertiesWhenOn": "Aktiviert — ergänzt fehlende Vorlagenfelder aus properties.xml.",
            "modOpsWrapWhenOff": "Deaktiviert — Ausgabe bleibt rohes <Asset>-XML.",
            "modOpsWrapWhenOn": "Aktiviert — wickelt die Ausgabe in Mod Loader ModOps/ModOp-XML ein.",
            "templatesWhenOff": "Deaktiviert — alle Asset-Dateien bleiben in einem Ordner.",
            "templatesWhenOn": "Aktiviert — gruppiert Dateien in Unterordner nach Vorlagenname.",
            "splitTemplatesWhenOff": "Deaktiviert — templates.xml wird nicht als separate Dateien exportiert.",
            "splitTemplatesWhenOn": "Aktiviert — schreibt eine XML-Datei pro Vorlage nach output_templates.",
            "createAssetModsWhenOff": "Deaktiviert — es werden keine Mod-Loader-Ordner pro Asset erstellt.",
            "createAssetModsWhenOn": "Aktiviert — erstellt einen kopierfertigen Mod-Loader-Ordner pro Asset.\n\nOptional: Vorlagenordner aktivieren, um Mods nach Vorlage zu gruppieren; sonst Ordner nach GUID und Asset-Name.",
            "debugModeWhenOff": "Deaktiviert — normale Konsolenausgabe.",
            "debugModeWhenOn": "Aktiviert — ausführliches Protokoll mit Backend-Argumenten und Dateidetails.",
        },
    },
    "fr": {
        "labels": {
            "gameDirectory": "Jeu :",
            "outputDirectory": "Sortie :",
            "singleGuid": "GUID :",
            "noLanguagesDetected": "Aucune",
            "detectedInstallations": "Install :",
        },
        "placeholders": {"singleGuid": "Tous les assets"},
        "descriptions": {
            "firstRunHint": "Choisissez le dossier du jeu (détection auto ou Parcourir), définissez la sortie, puis cliquez sur Extraire les fichiers du jeu. Les options se débloquent après l'extraction RDA.",
            "firstRunStep1": "Détection auto ou Parcourir",
            "firstRunStep2": "Choisir le dossier de sortie",
            "firstRunStep3": "Extraire les fichiers du jeu",
            "language": "Commentaires GUID uniquement",
            "singleGuid": "Optionnel — un asset par GUID",
        },
        "checkboxes": {
            "comments": "Commentaires GUID",
            "dependencies": "Résoudre les parents",
            "includeDefaultProperties": "Propriétés par défaut",
            "modOpsWrap": "XML ModOps",
            "templates": "Dossiers de modèles",
            "splitTemplates": "Diviser les modèles",
            "createAssetMods": "Dossiers mod par asset",
            "debugMode": "Journal détaillé",
        },
        "dialogs": {
            "selectLanguageForComments": "Commentaires GUID activés — sélectionnez une langue de jeu (texts_*.xml) avant de traiter.",
            "noLanguagesForComments": "Commentaires GUID activés, mais aucun fichier texts_*.xml trouvé.\n\nLancez d'abord l'extraction pour obtenir les fichiers de langue depuis les archives RDA.",
        },
        "statusMessages": {
            "invalidGameDirectory": "Dossier Anno invalide — choisissez celui qui contient maindata avec des fichiers .rda (ex. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "Plusieurs jeux Anno dans ce dossier — sélectionnez-en un ci-dessous",
            "languageSetupBannerTitle": "Les commentaires GUID nécessitent une langue de jeu",
            "languageSetupBannerDetail": "Sélectionnez un texts_*.xml à gauche ou désactivez les commentaires GUID pour traiter sans traductions.",
        },
        "tooltips": {
            "extract": "Étape 3 au premier lancement : extraire les fichiers bruts des archives RDA. Ensuite : traiter les assets avec les options à gauche.",
            "processingOptionsWhenLocked": "Verrouillé jusqu'à l'extraction des fichiers du jeu.\n\nDéfinissez les chemins jeu et sortie, puis Extrayez. Ces options se débloquent une fois source_xml détecté.",
            "processingOptionsWhenReady": "Configurez le traitement des assets extraits, puis cliquez sur Traiter les assets.",
            "singleGuid": "En général, laissez vide pour extraire tous les assets.\n\nPour un seul asset, entrez son GUID numérique (ex. 1010017). Un aperçu s'affiche s'il est trouvé dans assets.xml.\n\nLe mode GUID unique désactive dossiers de modèles et division des modèles. Résoudre les parents peut toujours fusionner les données parentes.",
            "languageWhenOff": "Langue du jeu verrouillée.\n\nActivez Commentaires GUID pour choisir un texts_*.xml. Les autres options fonctionnent sans langue de jeu.",
            "languageWhenOn": "Choisissez un texts_*.xml pour les commentaires GUID traduits.\n\nSi la liste est vide, lancez d'abord l'extraction.",
            "commentsWhenOff": "Désactivé — pas de commentaires de noms traduits dans le XML.\n\nLa langue du jeu reste verrouillée.",
            "commentsWhenOn": "Activé — ajoute des noms traduits à côté de chaque GUID.\n\nDébloque aussi la langue du jeu ci-dessus.",
            "dependenciesWhenOff": "Désactivé — les données parent BaseAssetGUID ne sont pas fusionnées.",
            "dependenciesWhenOn": "Activé — fusionne les chaînes BaseAssetGUID héritées dans chaque asset.",
            "includeDefaultPropertiesWhenOff": "Désactivé — les champs manquants ne sont pas remplis depuis properties.xml.",
            "includeDefaultPropertiesWhenOn": "Activé — remplit les champs manquants depuis properties.xml.",
            "modOpsWrapWhenOff": "Désactivé — la sortie reste du XML <Asset> brut.",
            "modOpsWrapWhenOn": "Activé — enveloppe la sortie en XML ModOps/ModOp Mod Loader.",
            "templatesWhenOff": "Désactivé — tous les fichiers restent dans un seul dossier.",
            "templatesWhenOn": "Activé — regroupe les fichiers par nom de modèle.",
            "splitTemplatesWhenOff": "Désactivé — templates.xml n'est pas exporté en fichiers séparés.",
            "splitTemplatesWhenOn": "Activé — écrit un XML par modèle dans output_templates.",
            "createAssetModsWhenOff": "Désactivé — aucun dossier Mod Loader par asset.",
            "createAssetModsWhenOn": "Activé — crée un dossier Mod Loader prêt à copier par asset.\n\nOptionnel : activez Dossiers de modèles pour grouper par modèle ; sinon dossiers par GUID et nom.",
            "debugModeWhenOff": "Désactivé — sortie console normale.",
            "debugModeWhenOn": "Activé — journal détaillé avec arguments backend et détails par fichier.",
        },
    },
    "es": {
        "labels": {
            "gameDirectory": "Juego:",
            "outputDirectory": "Salida:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "Aún ninguna",
            "detectedInstallations": "Instalar:",
        },
        "placeholders": {"singleGuid": "Todos los assets"},
        "descriptions": {
            "firstRunHint": "Elige la carpeta del juego (Auto-detectar o Examinar), define la salida y pulsa Extraer archivos del juego. Las opciones se desbloquean tras la extracción RDA.",
            "firstRunStep1": "Auto-detectar o Examinar",
            "firstRunStep2": "Elegir carpeta de salida",
            "firstRunStep3": "Extraer archivos del juego",
            "language": "Solo comentarios GUID",
            "singleGuid": "Opcional — un asset por GUID",
        },
        "checkboxes": {
            "comments": "Comentarios GUID",
            "dependencies": "Resolver padres",
            "includeDefaultProperties": "Propiedades predeterminadas",
            "modOpsWrap": "XML ModOps",
            "templates": "Carpetas de plantillas",
            "splitTemplates": "Dividir plantillas",
            "createAssetMods": "Carpetas mod por asset",
            "debugMode": "Registro detallado",
        },
        "dialogs": {
            "selectLanguageForComments": "Comentarios GUID activados — selecciona un idioma del juego (texts_*.xml) antes de procesar.",
            "noLanguagesForComments": "Comentarios GUID activados, pero no se encontraron archivos texts_*.xml.\n\nEjecuta primero la extracción para obtener los archivos de idioma de los archivos RDA.",
        },
        "statusMessages": {
            "invalidGameDirectory": "No es una carpeta Anno válida — elige la que contiene maindata con archivos .rda (p. ej. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "Varios juegos Anno en esta carpeta — selecciona uno abajo",
            "languageSetupBannerTitle": "Los comentarios GUID necesitan un idioma del juego",
            "languageSetupBannerDetail": "Selecciona un texts_*.xml a la izquierda o desactiva Comentarios GUID para procesar sin traducciones.",
        },
        "tooltips": {
            "extract": "Paso 3 en el primer inicio: extraer archivos crudos de los RDA. Después: procesar assets con las opciones de la izquierda.",
            "processingOptionsWhenLocked": "Bloqueado hasta extraer los archivos del juego.\n\nDefine rutas de juego y salida, luego Extraer. Estas opciones se desbloquean cuando source_xml esté en disco.",
            "processingOptionsWhenReady": "Configura cómo procesar los assets extraídos y pulsa Procesar assets.",
            "singleGuid": "Normalmente déjalo vacío para extraer todos los assets.\n\nPara uno solo, introduce su GUID numérico (ej. 1010017). Aparece una vista previa si está en assets.xml.\n\nEl modo GUID único desactiva carpetas de plantillas y división de plantillas. Resolver padres puede seguir fusionando datos del padre.",
            "languageWhenOff": "Idioma del juego bloqueado.\n\nActiva Comentarios GUID para elegir un texts_*.xml. El resto funciona sin idioma del juego.",
            "languageWhenOn": "Elige un texts_*.xml para comentarios GUID traducidos.\n\nSi la lista está vacía, ejecuta primero la extracción.",
            "commentsWhenOff": "Desactivado — sin comentarios de nombres traducidos en el XML.\n\nEl idioma del juego sigue bloqueado.",
            "commentsWhenOn": "Activado — añade nombres traducidos junto a cada GUID.\n\nTambién desbloquea el idioma del juego arriba.",
            "dependenciesWhenOff": "Desactivado — no se fusionan datos padre BaseAssetGUID.",
            "dependenciesWhenOn": "Activado — fusiona cadenas BaseAssetGUID heredadas en cada asset.",
            "includeDefaultPropertiesWhenOff": "Desactivado — no rellena campos faltantes desde properties.xml.",
            "includeDefaultPropertiesWhenOn": "Activado — rellena campos faltantes desde properties.xml.",
            "modOpsWrapWhenOff": "Desactivado — la salida sigue siendo XML <Asset> crudo.",
            "modOpsWrapWhenOn": "Activado — envuelve la salida en XML ModOps/ModOp de Mod Loader.",
            "templatesWhenOff": "Desactivado — todos los archivos en una sola carpeta.",
            "templatesWhenOn": "Activado — agrupa archivos en subcarpetas por plantilla.",
            "splitTemplatesWhenOff": "Desactivado — templates.xml no se exporta en archivos separados.",
            "splitTemplatesWhenOn": "Activado — escribe un XML por plantilla en output_templates.",
            "createAssetModsWhenOff": "Desactivado — no crea carpetas Mod Loader por asset.",
            "createAssetModsWhenOn": "Activado — crea una carpeta Mod Loader lista para copiar por asset.\n\nOpcional: activa Carpetas de plantillas para agrupar por plantilla; si no, carpetas por GUID y nombre.",
            "debugModeWhenOff": "Desactivado — salida normal de consola.",
            "debugModeWhenOn": "Activado — registro detallado con argumentos del backend y detalles por archivo.",
        },
    },
    "it": {
        "labels": {
            "gameDirectory": "Gioco:",
            "outputDirectory": "Output:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "Nessuna",
            "detectedInstallations": "Install:",
        },
        "placeholders": {"singleGuid": "Tutti gli asset"},
        "descriptions": {
            "firstRunHint": "Scegli la cartella di gioco (Rileva automaticamente o Sfoglia), imposta l'output, poi clicca Estrai file di gioco. Le opzioni si sbloccano dopo l'estrazione RDA.",
            "firstRunStep1": "Rileva automaticamente o Sfoglia",
            "firstRunStep2": "Scegli cartella di output",
            "firstRunStep3": "Estrai file di gioco",
            "language": "Solo commenti GUID",
            "singleGuid": "Opzionale — un asset per GUID",
        },
        "checkboxes": {
            "comments": "Commenti GUID",
            "dependencies": "Risolvi genitori",
            "includeDefaultProperties": "Proprietà predefinite",
            "modOpsWrap": "XML ModOps",
            "templates": "Cartelle template",
            "splitTemplates": "Dividi template",
            "createAssetMods": "Cartelle mod asset",
            "debugMode": "Log dettagliato",
        },
        "dialogs": {
            "selectLanguageForComments": "Commenti GUID attivi — seleziona una lingua di gioco (texts_*.xml) prima di elaborare.",
            "noLanguagesForComments": "Commenti GUID attivi, ma nessun file texts_*.xml trovato.\n\nEsegui prima l'estrazione per ottenere i file lingua dagli archivi RDA.",
        },
        "statusMessages": {
            "invalidGameDirectory": "Cartella Anno non valida — scegli quella con maindata e file .rda (es. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "Più giochi Anno in questa cartella — selezionane uno sotto",
            "languageSetupBannerTitle": "I commenti GUID richiedono una lingua di gioco",
            "languageSetupBannerDetail": "Seleziona un texts_*.xml a sinistra o disattiva Commenti GUID per elaborare senza traduzioni.",
        },
        "tooltips": {
            "extract": "Passo 3 al primo avvio: estrai i file grezzi dagli archivi RDA. Poi: elabora gli asset con le opzioni a sinistra.",
            "processingOptionsWhenLocked": "Bloccato finché i file di gioco non sono estratti.\n\nImposta percorsi gioco e output, poi Estrai. Queste opzioni si sbloccano quando source_xml è su disco.",
            "processingOptionsWhenReady": "Configura l'elaborazione degli asset estratti, poi clicca Elabora asset.",
            "singleGuid": "Di solito lascia vuoto per estrarre tutti gli asset.\n\nPer uno solo, inserisci il GUID numerico (es. 1010017). Compare un'anteprima se trovato in assets.xml.\n\nLa modalità GUID singolo disattiva cartelle template e divisione template. Risolvi genitori può ancora unire i dati del genitore.",
            "languageWhenOff": "Lingua di gioco bloccata.\n\nAttiva Commenti GUID per scegliere un texts_*.xml. Le altre opzioni funzionano senza lingua di gioco.",
            "languageWhenOn": "Scegli un texts_*.xml per commenti GUID tradotti.\n\nSe l'elenco è vuoto, esegui prima l'estrazione.",
            "commentsWhenOff": "Disattivato — nessun commento con nomi tradotti nel XML.\n\nLa lingua di gioco resta bloccata.",
            "commentsWhenOn": "Attivato — aggiunge nomi tradotti accanto a ogni GUID.\n\nSblocca anche la lingua di gioco sopra.",
            "dependenciesWhenOff": "Disattivato — i dati genitore BaseAssetGUID non vengono uniti.",
            "dependenciesWhenOn": "Attivato — unisce le catene BaseAssetGUID ereditate in ogni asset.",
            "includeDefaultPropertiesWhenOff": "Disattivato — i campi mancanti non vengono riempiti da properties.xml.",
            "includeDefaultPropertiesWhenOn": "Attivato — riempie i campi mancanti da properties.xml.",
            "modOpsWrapWhenOff": "Disattivato — l'output resta XML <Asset> grezzo.",
            "modOpsWrapWhenOn": "Attivato — avvolge l'output in XML ModOps/ModOp Mod Loader.",
            "templatesWhenOff": "Disattivato — tutti i file in un'unica cartella.",
            "templatesWhenOn": "Attivato — raggruppa i file in sottocartelle per template.",
            "splitTemplatesWhenOff": "Disattivato — templates.xml non viene esportato in file separati.",
            "splitTemplatesWhenOn": "Attivato — scrive un XML per template in output_templates.",
            "createAssetModsWhenOff": "Disattivato — nessuna cartella Mod Loader per asset.",
            "createAssetModsWhenOn": "Attivato — crea una cartella Mod Loader pronta da copiare per asset.\n\nOpzionale: attiva Cartelle template per raggruppare per template; altrimenti cartelle per GUID e nome.",
            "debugModeWhenOff": "Disattivato — output console normale.",
            "debugModeWhenOn": "Attivato — log dettagliato con argomenti backend e dettagli per file.",
        },
    },
    "pl": {
        "labels": {
            "gameDirectory": "Gra:",
            "outputDirectory": "Wyjście:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "Jeszcze brak",
            "detectedInstallations": "Inst.:",
        },
        "placeholders": {"singleGuid": "Wszystkie assety"},
        "descriptions": {
            "firstRunHint": "Wybierz folder gry (Autowykrywanie lub Przeglądaj), ustaw folder wyjściowy, potem kliknij Wyodrębnij pliki gry. Opcje przetwarzania odblokują się po ekstrakcji RDA.",
            "firstRunStep1": "Autowykrywanie lub Przeglądaj",
            "firstRunStep2": "Wybierz folder wyjściowy",
            "firstRunStep3": "Wyodrębnij pliki gry",
            "language": "Tylko komentarze GUID",
            "singleGuid": "Opcjonalnie — jeden asset po GUID",
        },
        "checkboxes": {
            "comments": "Komentarze GUID",
            "dependencies": "Rozwiąż rodziców",
            "includeDefaultProperties": "Właściwości domyślne",
            "modOpsWrap": "XML ModOps",
            "templates": "Foldery szablonów",
            "splitTemplates": "Podziel szablony",
            "createAssetMods": "Foldery modów assetów",
            "debugMode": "Szczegółowy log",
        },
        "dialogs": {
            "selectLanguageForComments": "Komentarze GUID włączone — wybierz język gry (texts_*.xml) przed przetwarzaniem.",
            "noLanguagesForComments": "Komentarze GUID włączone, ale nie znaleziono plików texts_*.xml.\n\nNajpierw uruchom ekstrakcję, aby pobrać pliki językowe z archiwów RDA.",
        },
        "statusMessages": {
            "invalidGameDirectory": "Nieprawidłowy folder Anno — wybierz folder z maindata i plikami .rda (np. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "Wiele gier Anno w tym folderze — wybierz jedną poniżej",
            "languageSetupBannerTitle": "Komentarze GUID wymagają języka gry",
            "languageSetupBannerDetail": "Wybierz texts_*.xml po lewej lub wyłącz Komentarze GUID, aby przetwarzać bez tłumaczeń.",
        },
        "tooltips": {
            "extract": "Krok 3 przy pierwszym uruchomieniu: wyodrębnij surowe pliki z archiwów RDA. Później: przetwarzaj assety opcjami po lewej.",
            "processingOptionsWhenLocked": "Zablokowane do czasu wyodrębnienia plików gry.\n\nUstaw ścieżki gry i wyjścia, potem Wyodrębnij. Opcje odblokują się, gdy source_xml będzie na dysku.",
            "processingOptionsWhenReady": "Skonfiguruj przetwarzanie wyodrębnionych assetów, potem kliknij Przetwórz assety.",
            "singleGuid": "Zwykle zostaw puste, aby wyodrębnić wszystkie assety.\n\nDla jednego wpisz numeryczny GUID (np. 1010017). Podgląd pojawi się, jeśli GUID jest w assets.xml.\n\nTryb pojedynczego GUID wyłącza foldery szablonów i podział szablonów. Rozwiąż rodziców nadal może scalać dane rodzica.",
            "languageWhenOff": "Język gry zablokowany.\n\nWłącz Komentarze GUID, aby wybrać texts_*.xml. Pozostałe opcje działają bez języka gry.",
            "languageWhenOn": "Wybierz texts_*.xml dla przetłumaczonych komentarzy GUID.\n\nJeśli lista jest pusta, najpierw uruchom ekstrakcję.",
            "commentsWhenOff": "Wyłączone — brak przetłumaczonych komentarzy nazw w XML.\n\nJęzyk gry pozostaje zablokowany.",
            "commentsWhenOn": "Włączone — dodaje przetłumaczone nazwy obok każdego GUID.\n\nOdblokowuje też język gry powyżej.",
            "dependenciesWhenOff": "Wyłączone — dane rodzica BaseAssetGUID nie są scalane.",
            "dependenciesWhenOn": "Włączone — scala dziedziczone łańcuchy BaseAssetGUID w każdy asset.",
            "includeDefaultPropertiesWhenOff": "Wyłączone — brak uzupełniania pól z properties.xml.",
            "includeDefaultPropertiesWhenOn": "Włączone — uzupełnia brakujące pola z properties.xml.",
            "modOpsWrapWhenOff": "Wyłączone — wyjście pozostaje surowym XML <Asset>.",
            "modOpsWrapWhenOn": "Włączone — owija wyjście w XML ModOps/ModOp Mod Loader.",
            "templatesWhenOff": "Wyłączone — wszystkie pliki w jednym folderze.",
            "templatesWhenOn": "Włączone — grupuje pliki w podfoldery według szablonu.",
            "splitTemplatesWhenOff": "Wyłączone — templates.xml nie jest eksportowany jako osobne pliki.",
            "splitTemplatesWhenOn": "Włączone — zapisuje jeden XML na szablon w output_templates.",
            "createAssetModsWhenOff": "Wyłączone — brak folderów Mod Loader na asset.",
            "createAssetModsWhenOn": "Włączone — tworzy gotowy folder Mod Loader na asset.\n\nOpcjonalnie: włącz Foldery szablonów, aby grupować po szablonie; inaczej foldery według GUID i nazwy.",
            "debugModeWhenOff": "Wyłączone — normalne wyjście konsoli.",
            "debugModeWhenOn": "Włączone — szczegółowy log z argumentami backendu i detalami plików.",
        },
    },
    "ru": {
        "labels": {
            "gameDirectory": "Игра:",
            "outputDirectory": "Вывод:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "Пока нет",
            "detectedInstallations": "Уст.:",
        },
        "placeholders": {"singleGuid": "Все ассеты"},
        "descriptions": {
            "firstRunHint": "Выберите папку игры (Автоопределение или Обзор), укажите вывод и нажмите «Извлечь файлы игры». Опции обработки откроются после извлечения RDA.",
            "firstRunStep1": "Автоопределение или Обзор",
            "firstRunStep2": "Выберите папку вывода",
            "firstRunStep3": "Извлечь файлы игры",
            "language": "Только комментарии GUID",
            "singleGuid": "Необязательно — один ассет по GUID",
        },
        "checkboxes": {
            "comments": "Комментарии GUID",
            "dependencies": "Разрешить родителей",
            "includeDefaultProperties": "Свойства по умолчанию",
            "modOpsWrap": "XML ModOps",
            "templates": "Папки шаблонов",
            "splitTemplates": "Разделить шаблоны",
            "createAssetMods": "Папки модов ассетов",
            "debugMode": "Подробный лог",
        },
        "dialogs": {
            "selectLanguageForComments": "Комментарии GUID включены — выберите язык игры (texts_*.xml) перед обработкой.",
            "noLanguagesForComments": "Комментарии GUID включены, но файлы texts_*.xml не найдены.\n\nСначала выполните извлечение, чтобы получить языковые файлы из RDA-архивов.",
        },
        "statusMessages": {
            "invalidGameDirectory": "Неверная папка Anno — выберите папку с maindata и файлами .rda (напр. …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "В этой папке несколько игр Anno — выберите одну ниже",
            "languageSetupBannerTitle": "Комментариям GUID нужен язык игры",
            "languageSetupBannerDetail": "Выберите texts_*.xml слева или отключите Комментарии GUID для обработки без переводов.",
        },
        "tooltips": {
            "extract": "Шаг 3 при первом запуске: извлечь сырые файлы из RDA. Затем: обработать ассеты с опциями слева.",
            "processingOptionsWhenLocked": "Заблокировано до извлечения файлов игры.\n\nУкажите пути игры и вывода, затем Извлечь. Опции откроются, когда source_xml появится на диске.",
            "processingOptionsWhenReady": "Настройте обработку извлечённых ассетов и нажмите «Обработать ассеты».",
            "singleGuid": "Обычно оставьте пустым — извлекутся все ассеты.\n\nДля одного укажите числовой GUID (напр. 1010017). Превью появится, если GUID найден в assets.xml.\n\nРежим одного GUID отключает папки шаблонов и разделение шаблонов. Разрешение родителей может объединять данные родителя.",
            "languageWhenOff": "Язык игры заблокирован.\n\nВключите Комментарии GUID, чтобы выбрать texts_*.xml. Остальные опции работают без языка игры.",
            "languageWhenOn": "Выберите texts_*.xml для переведённых комментариев GUID.\n\nЕсли список пуст, сначала выполните извлечение.",
            "commentsWhenOff": "Выключено — без переведённых комментариев имён в XML.\n\nЯзык игры остаётся заблокированным.",
            "commentsWhenOn": "Включено — добавляет переведённые имена рядом с каждым GUID.\n\nТакже разблокирует язык игры выше.",
            "dependenciesWhenOff": "Выключено — данные родителя BaseAssetGUID не объединяются.",
            "dependenciesWhenOn": "Включено — объединяет унаследованные цепочки BaseAssetGUID в каждый ассет.",
            "includeDefaultPropertiesWhenOff": "Выключено — пропуски не заполняются из properties.xml.",
            "includeDefaultPropertiesWhenOn": "Включено — заполняет пропуски из properties.xml.",
            "modOpsWrapWhenOff": "Выключено — вывод остаётся сырым XML <Asset>.",
            "modOpsWrapWhenOn": "Включено — оборачивает вывод в XML ModOps/ModOp Mod Loader.",
            "templatesWhenOff": "Выключено — все файлы в одной папке.",
            "templatesWhenOn": "Включено — группирует файлы в подпапки по шаблону.",
            "splitTemplatesWhenOff": "Выключено — templates.xml не экспортируется отдельными файлами.",
            "splitTemplatesWhenOn": "Включено — пишет один XML на шаблон в output_templates.",
            "createAssetModsWhenOff": "Выключено — папки Mod Loader на ассет не создаются.",
            "createAssetModsWhenOn": "Включено — создаёт готовую папку Mod Loader на ассет.\n\nОпционально: включите Папки шаблонов для группировки; иначе папки по GUID и имени.",
            "debugModeWhenOff": "Выключено — обычный вывод консоли.",
            "debugModeWhenOn": "Включено — подробный лог с аргументами backend и деталями по файлам.",
        },
    },
    "ja": {
        "labels": {
            "gameDirectory": "ゲーム:",
            "outputDirectory": "出力:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "まだなし",
            "detectedInstallations": "インストール:",
        },
        "placeholders": {"singleGuid": "すべてのアセット"},
        "descriptions": {
            "firstRunHint": "ゲームフォルダを選び（自動検出または参照）、出力先を設定し、「ゲームファイルを抽出」をクリック。RDA 抽出後に処理オプションが有効になります。",
            "firstRunStep1": "自動検出または参照",
            "firstRunStep2": "出力フォルダを選択",
            "firstRunStep3": "ゲームファイルを抽出",
            "language": "GUID コメントのみ",
            "singleGuid": "任意 — GUID で 1 アセット",
        },
        "checkboxes": {
            "comments": "GUID コメント",
            "dependencies": "親を解決",
            "includeDefaultProperties": "デフォルト属性",
            "modOpsWrap": "ModOps XML",
            "templates": "テンプレートフォルダ",
            "splitTemplates": "テンプレート分割",
            "createAssetMods": "アセット mod フォルダ",
            "debugMode": "詳細ログ",
        },
        "dialogs": {
            "selectLanguageForComments": "GUID コメントが有効 — 処理前にゲーム言語（texts_*.xml）を選択してください。",
            "noLanguagesForComments": "GUID コメントが有効ですが texts_*.xml が見つかりません。\n\nRDA アーカイブから言語ファイルを取得するため、先に抽出を実行してください。",
        },
        "statusMessages": {
            "invalidGameDirectory": "有効な Anno フォルダではありません — maindata と .rda があるフォルダを選んでください（例: …\\Anno 117 - Pax Romana）。",
            "selectGameSubfolder": "このフォルダに複数の Anno があります — 下から選択してください",
            "languageSetupBannerTitle": "GUID コメントにはゲーム言語が必要です",
            "languageSetupBannerDetail": "左で texts_*.xml を選ぶか、GUID コメントをオフにして翻訳なしで処理してください。",
        },
        "tooltips": {
            "extract": "初回のステップ 3: RDA から生ファイルを抽出。以降は左のオプションでアセットを処理。",
            "processingOptionsWhenLocked": "ゲームファイル抽出までロック中。\n\nゲームと出力パスを設定して抽出。source_xml が検出されるとオプションが有効になります。",
            "processingOptionsWhenReady": "抽出アセットの処理方法を設定し、「アセットを処理」をクリック。",
            "singleGuid": "通常は空のまま — 全アセットを抽出。\n\n1 件だけなら数値 GUID（例: 1010017）を入力。assets.xml にあればプレビュー表示。\n\n単一 GUID モードではテンプレートフォルダと分割を無効化。親の解決は親データのマージが可能。",
            "languageWhenOff": "ゲーム言語はロック中。\n\nGUID コメントをオンにして texts_*.xml を選択。他のオプションは言語なしで動作。",
            "languageWhenOn": "翻訳 GUID コメント用の texts_*.xml を選択。\n\nリストが空なら先に抽出を実行。",
            "commentsWhenOff": "オフ — XML に翻訳名コメントなし。\n\nゲーム言語はロックのまま。",
            "commentsWhenOn": "オン — 各 GUID 横に翻訳名を追加。\n\n上のゲーム言語もアンロック。",
            "dependenciesWhenOff": "オフ — BaseAssetGUID 親データはマージされません。",
            "dependenciesWhenOn": "オン — 継承 BaseAssetGUID チェーンを各アセットにマージ。",
            "includeDefaultPropertiesWhenOff": "オフ — properties.xml から欠落フィールドを補完しません。",
            "includeDefaultPropertiesWhenOn": "オン — properties.xml のデフォルトで欠落を補完。",
            "modOpsWrapWhenOff": "オフ — 生の <Asset> XML のまま。",
            "modOpsWrapWhenOn": "オン — Mod Loader ModOps/ModOp XML でラップ。",
            "templatesWhenOff": "オフ — 全ファイルは 1 フォルダ。",
            "templatesWhenOn": "オン — テンプレート名のサブフォルダに整理。",
            "splitTemplatesWhenOff": "オフ — templates.xml を個別ファイル出力しません。",
            "splitTemplatesWhenOn": "オン — output_templates にテンプレートごと 1 XML。",
            "createAssetModsWhenOff": "オフ — アセットごとの Mod Loader フォルダなし。",
            "createAssetModsWhenOn": "オン — アセットごとにコピー可能な Mod Loader フォルダを作成。\n\n任意: テンプレートフォルダをオンにしてテンプレート別にグループ化。そうでなければ GUID とアセット名でフォルダ。",
            "debugModeWhenOff": "オフ — 通常のコンソール出力。",
            "debugModeWhenOn": "オン — バックエンド引数とファイル詳細の詳細ログ。",
        },
    },
    "ko": {
        "labels": {
            "gameDirectory": "게임:",
            "outputDirectory": "출력:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "아직 없음",
            "detectedInstallations": "설치:",
        },
        "placeholders": {"singleGuid": "모든 에셋"},
        "descriptions": {
            "firstRunHint": "게임 폴더 선택(자동 감지 또는 찾아보기), 출력 폴더 설정 후 '게임 파일 추출' 클릭. RDA 추출 후 처리 옵션이 잠금 해제됩니다.",
            "firstRunStep1": "자동 감지 또는 찾아보기",
            "firstRunStep2": "출력 폴더 선택",
            "firstRunStep3": "게임 파일 추출",
            "language": "GUID 주석만",
            "singleGuid": "선택 — GUID로 하나의 에셋",
        },
        "checkboxes": {
            "comments": "GUID 주석",
            "dependencies": "부모 해결",
            "includeDefaultProperties": "기본 속성",
            "modOpsWrap": "ModOps XML",
            "templates": "템플릿 폴더",
            "splitTemplates": "템플릿 분할",
            "createAssetMods": "에셋 mod 폴더",
            "debugMode": "상세 로그",
        },
        "dialogs": {
            "selectLanguageForComments": "GUID 주석이 켜져 있습니다 — 처리 전 게임 언어(texts_*.xml)를 선택하세요.",
            "noLanguagesForComments": "GUID 주석이 켜져 있지만 texts_*.xml 파일이 없습니다.\n\nRDA 아카이브에서 언어 파일을 얻으려면 먼저 추출을 실행하세요.",
        },
        "statusMessages": {
            "invalidGameDirectory": "유효한 Anno 폴더가 아닙니다 — maindata와 .rda가 있는 폴더를 선택하세요(예: …\\Anno 117 - Pax Romana).",
            "selectGameSubfolder": "이 폴더에 Anno 게임이 여러 개 있습니다 — 아래에서 선택하세요",
            "languageSetupBannerTitle": "GUID 주석에는 게임 언어가 필요합니다",
            "languageSetupBannerDetail": "왼쪽에서 texts_*.xml을 선택하거나 GUID 주석을 끄고 번역 없이 처리하세요.",
        },
        "tooltips": {
            "extract": "첫 실행 3단계: RDA에서 원본 파일 추출. 이후: 왼쪽 옵션으로 에셋 처리.",
            "processingOptionsWhenLocked": "게임 파일 추출 전까지 잠김.\n\n게임 및 출력 경로 설정 후 추출. source_xml이 있으면 옵션이 잠금 해제됩니다.",
            "processingOptionsWhenReady": "추출된 에셋 처리 방법을 설정한 뒤 '에셋 처리'를 클릭하세요.",
            "singleGuid": "보통 비워 두면 모든 에셋을 추출합니다.\n\n하나만 추출하려면 숫자 GUID(예: 1010017) 입력. assets.xml에 있으면 미리보기 표시.\n\n단일 GUID 모드는 템플릿 폴더와 분할을 끕니다. 부모 해결은 부모 데이터 병합 가능.",
            "languageWhenOff": "게임 언어 잠김.\n\nGUID 주석을 켜서 texts_*.xml 선택. 다른 옵션은 게임 언어 없이 동작.",
            "languageWhenOn": "번역 GUID 주석용 texts_*.xml 선택.\n\n목록이 비어 있으면 먼저 추출 실행.",
            "commentsWhenOff": "끔 — XML에 번역 이름 주석 없음.\n\n게임 언어는 잠긴 상태.",
            "commentsWhenOn": "켬 — 각 GUID 옆에 번역 이름 추가.\n\n위 게임 언어도 잠금 해제.",
            "dependenciesWhenOff": "끔 — BaseAssetGUID 부모 데이터 병합 안 함.",
            "dependenciesWhenOn": "켬 — 상속 BaseAssetGUID 체인을 각 에셋에 병합.",
            "includeDefaultPropertiesWhenOff": "끔 — properties.xml에서 누락 필드 채우지 않음.",
            "includeDefaultPropertiesWhenOn": "켬 — properties.xml 기본값으로 누락 채움.",
            "modOpsWrapWhenOff": "끔 — 원시 <Asset> XML 유지.",
            "modOpsWrapWhenOn": "켬 — Mod Loader ModOps/ModOp XML로 래핑.",
            "templatesWhenOff": "끔 — 모든 파일이 한 폴더.",
            "templatesWhenOn": "켬 — 템플릿 이름별 하위 폴더로 그룹.",
            "splitTemplatesWhenOff": "끔 — templates.xml을 별도 파일로 내보내지 않음.",
            "splitTemplatesWhenOn": "켬 — output_templates에 템플릿당 XML 하나.",
            "createAssetModsWhenOff": "끔 — 에셋별 Mod Loader 폴더 없음.",
            "createAssetModsWhenOn": "켬 — 에셋마다 복사 가능한 Mod Loader 폴더 생성.\n\n선택: 템플릿 폴더로 mod 그룹화; 아니면 GUID와 이름으로 폴더.",
            "debugModeWhenOff": "끔 — 일반 콘솔 출력.",
            "debugModeWhenOn": "켬 — 백엔드 인수 및 파일별 상세 로그.",
        },
    },
    "zh": {
        "labels": {
            "gameDirectory": "游戏:",
            "outputDirectory": "输出:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "暂无",
            "detectedInstallations": "安装:",
        },
        "placeholders": {"singleGuid": "全部资产"},
        "descriptions": {
            "firstRunHint": "选择游戏文件夹（自动检测或浏览），设置输出文件夹，然后点击“提取游戏文件”。RDA 提取后解锁处理选项。",
            "firstRunStep1": "自动检测或浏览",
            "firstRunStep2": "选择输出文件夹",
            "firstRunStep3": "提取游戏文件",
            "language": "仅 GUID 注释",
            "singleGuid": "可选 — 按 GUID 提取单个资产",
        },
        "checkboxes": {
            "comments": "GUID 注释",
            "dependencies": "解析父级",
            "includeDefaultProperties": "默认属性",
            "modOpsWrap": "ModOps XML",
            "templates": "模板文件夹",
            "splitTemplates": "拆分模板文件",
            "createAssetMods": "资产 mod 文件夹",
            "debugMode": "详细日志",
        },
        "dialogs": {
            "selectLanguageForComments": "已启用 GUID 注释 — 处理前请选择游戏语言（texts_*.xml）。",
            "noLanguagesForComments": "已启用 GUID 注释，但未找到 texts_*.xml 文件。\n\n请先运行提取，从 RDA 归档中获取语言文件。",
        },
        "statusMessages": {
            "invalidGameDirectory": "不是有效的 Anno 文件夹 — 请选择包含 maindata 和 .rda 文件的文件夹（例如 …\\Anno 117 - Pax Romana）。",
            "selectGameSubfolder": "此文件夹中有多个 Anno 游戏 — 请在下方选择一个",
            "languageSetupBannerTitle": "GUID 注释需要游戏语言",
            "languageSetupBannerDetail": "在左侧选择 texts_*.xml，或关闭 GUID 注释以无翻译方式处理。",
        },
        "tooltips": {
            "extract": "首次运行第 3 步：从 RDA 提取原始游戏文件。之后：使用左侧选项处理资产。",
            "processingOptionsWhenLocked": "提取游戏文件前锁定。\n\n设置游戏和输出路径后点击提取。检测到 source_xml 后解锁这些选项。",
            "processingOptionsWhenReady": "配置提取资产的处理方式，然后点击“处理资产”。",
            "singleGuid": "通常留空以提取全部资产。\n\n仅提取一项时输入数字 GUID（如 1010017）。在 assets.xml 中找到会显示预览。\n\n单 GUID 模式会禁用模板文件夹和拆分模板。解析父级仍可合并父数据。",
            "languageWhenOff": "游戏语言已锁定。\n\n启用 GUID 注释以选择 texts_*.xml。其他选项无需游戏语言。",
            "languageWhenOn": "选择 texts_*.xml 以获取翻译 GUID 注释。\n\n列表为空时请先运行提取。",
            "commentsWhenOff": "关闭 — XML 中无翻译名称注释。\n\n游戏语言保持锁定。",
            "commentsWhenOn": "开启 — 在每个 GUID 旁添加翻译名称。\n\n同时解锁上方游戏语言。",
            "dependenciesWhenOff": "关闭 — 不合并 BaseAssetGUID 父数据。",
            "dependenciesWhenOn": "开启 — 将继承的 BaseAssetGUID 链合并到每个资产。",
            "includeDefaultPropertiesWhenOff": "关闭 — 不从 properties.xml 填充缺失字段。",
            "includeDefaultPropertiesWhenOn": "开启 — 用 properties.xml 默认值填充缺失字段。",
            "modOpsWrapWhenOff": "关闭 — 输出保持原始 <Asset> XML。",
            "modOpsWrapWhenOn": "开启 — 包装为 Mod Loader ModOps/ModOp XML。",
            "templatesWhenOff": "关闭 — 所有文件在同一文件夹。",
            "templatesWhenOn": "开启 — 按模板名称分子文件夹。",
            "splitTemplatesWhenOff": "关闭 — 不将 templates.xml 导出为单独文件。",
            "splitTemplatesWhenOn": "开启 — 在 output_templates 中每个模板一个 XML。",
            "createAssetModsWhenOff": "关闭 — 不创建每资产 Mod Loader 文件夹。",
            "createAssetModsWhenOn": "开启 — 为每个资产创建可复制的 Mod Loader 文件夹。\n\n可选：启用模板文件夹按模板分组；否则按 GUID 和资产名命名。",
            "debugModeWhenOff": "关闭 — 正常控制台输出。",
            "debugModeWhenOn": "开启 — 含后端参数和逐文件详情的详细日志。",
        },
    },
    "tw": {
        "labels": {
            "gameDirectory": "遊戲:",
            "outputDirectory": "輸出:",
            "singleGuid": "GUID:",
            "noLanguagesDetected": "尚無",
            "detectedInstallations": "安裝:",
        },
        "placeholders": {"singleGuid": "全部資產"},
        "descriptions": {
            "firstRunHint": "選擇遊戲資料夾（自動偵測或瀏覽），設定輸出資料夾，然後按「提取遊戲檔案」。RDA 提取後解鎖處理選項。",
            "firstRunStep1": "自動偵測或瀏覽",
            "firstRunStep2": "選擇輸出資料夾",
            "firstRunStep3": "提取遊戲檔案",
            "language": "僅 GUID 註解",
            "singleGuid": "選用 — 依 GUID 提取單一資產",
        },
        "checkboxes": {
            "comments": "GUID 註解",
            "dependencies": "解析父級",
            "includeDefaultProperties": "預設屬性",
            "modOpsWrap": "ModOps XML",
            "templates": "範本資料夾",
            "splitTemplates": "拆分範本檔",
            "createAssetMods": "資產 mod 資料夾",
            "debugMode": "詳細記錄",
        },
        "dialogs": {
            "selectLanguageForComments": "已啟用 GUID 註解 — 處理前請選擇遊戲語言（texts_*.xml）。",
            "noLanguagesForComments": "已啟用 GUID 註解，但未找到 texts_*.xml 檔案。\n\n請先執行提取，從 RDA 封存取得語言檔。",
        },
        "statusMessages": {
            "invalidGameDirectory": "不是有效的 Anno 資料夾 — 請選擇包含 maindata 與 .rda 的資料夾（例如 …\\Anno 117 - Pax Romana）。",
            "selectGameSubfolder": "此資料夾中有多個 Anno 遊戲 — 請在下方選擇",
            "languageSetupBannerTitle": "GUID 註解需要遊戲語言",
            "languageSetupBannerDetail": "在左側選擇 texts_*.xml，或關閉 GUID 註解以無翻譯方式處理。",
        },
        "tooltips": {
            "extract": "首次執行步驟 3：從 RDA 提取原始遊戲檔。之後：使用左側選項處理資產。",
            "processingOptionsWhenLocked": "提取遊戲檔案前鎖定。\n\n設定遊戲與輸出路徑後按提取。偵測到 source_xml 後解鎖這些選項。",
            "processingOptionsWhenReady": "設定提取資產的處理方式，然後按「處理資產」。",
            "singleGuid": "通常留空以提取全部資產。\n\n僅一項時輸入數字 GUID（如 1010017）。在 assets.xml 中找到會顯示預覽。\n\n單 GUID 模式會停用範本資料夾與拆分範本。解析父級仍可合併父資料。",
            "languageWhenOff": "遊戲語言已鎖定。\n\n啟用 GUID 註解以選擇 texts_*.xml。其他選項無需遊戲語言。",
            "languageWhenOn": "選擇 texts_*.xml 以取得翻譯 GUID 註解。\n\n清單為空時請先執行提取。",
            "commentsWhenOff": "關閉 — XML 中無翻譯名稱註解。\n\n遊戲語言保持鎖定。",
            "commentsWhenOn": "開啟 — 在每個 GUID 旁加入翻譯名稱。\n\n同時解鎖上方遊戲語言。",
            "dependenciesWhenOff": "關閉 — 不合併 BaseAssetGUID 父資料。",
            "dependenciesWhenOn": "開啟 — 將繼承的 BaseAssetGUID 鏈合併到每個資產。",
            "includeDefaultPropertiesWhenOff": "關閉 — 不從 properties.xml 填入缺失欄位。",
            "includeDefaultPropertiesWhenOn": "開啟 — 以 properties.xml 預設值填入缺失欄位。",
            "modOpsWrapWhenOff": "關閉 — 輸出保持原始 <Asset> XML。",
            "modOpsWrapWhenOn": "開啟 — 包裝為 Mod Loader ModOps/ModOp XML。",
            "templatesWhenOff": "關閉 — 所有檔案在同一資料夾。",
            "templatesWhenOn": "開啟 — 依範本名稱分子資料夾。",
            "splitTemplatesWhenOff": "關閉 — 不將 templates.xml 匯出為個別檔案。",
            "splitTemplatesWhenOn": "開啟 — 在 output_templates 中每個範本一個 XML。",
            "createAssetModsWhenOff": "關閉 — 不建立每資產 Mod Loader 資料夾。",
            "createAssetModsWhenOn": "開啟 — 為每個資產建立可複製的 Mod Loader 資料夾。\n\n選用：啟用範本資料夾依範本分組；否則依 GUID 與資產名命名。",
            "debugModeWhenOff": "關閉 — 一般主控台輸出。",
            "debugModeWhenOn": "開啟 — 含後端參數與逐檔詳情的詳細記錄。",
        },
    },
}

CONSOLE_PATCHES: dict[str, dict[str, str]] = {
    "de": {
        "commentsRequireLanguage": "GUID-Kommentare (-c) erfordern eine Spielsprache (texts_*.xml).",
        "sourceExtractionComplete": "[QUELLE BEREIT] Spieldateien nach source_xml extrahiert. Aktiviere eine Verarbeitungsoption und starte erneut, um Asset-XML zu erstellen.",
    },
    "fr": {
        "commentsRequireLanguage": "Les commentaires GUID (-c) nécessitent une langue de jeu (texts_*.xml).",
        "sourceExtractionComplete": "[SOURCE PRÊTE] Fichiers de jeu extraits vers source_xml. Activez une option de traitement et relancez pour générer le XML des assets.",
    },
    "es": {
        "commentsRequireLanguage": "Los comentarios GUID (-c) requieren un idioma de juego (texts_*.xml).",
        "sourceExtractionComplete": "[ORIGEN LISTO] Archivos de juego extraídos a source_xml. Activa una opción de procesamiento y vuelve a ejecutar para generar XML de assets.",
    },
    "it": {
        "commentsRequireLanguage": "I commenti GUID (-c) richiedono una lingua di gioco (texts_*.xml).",
        "sourceExtractionComplete": "[SORGENTE PRONTA] File di gioco estratti in source_xml. Abilita un'opzione di elaborazione e riesegui per creare l'XML degli asset.",
    },
    "pl": {
        "commentsRequireLanguage": "Komentarze GUID (-c) wymagają języka gry (texts_*.xml).",
        "sourceExtractionComplete": "[ŹRÓDŁO GOTOWE] Pliki gry wyodrębnione do source_xml. Włącz opcję przetwarzania i uruchom ponownie, aby utworzyć XML assetów.",
    },
    "ru": {
        "commentsRequireLanguage": "Комментарии GUID (-c) требуют язык игры (texts_*.xml).",
        "sourceExtractionComplete": "[ИСТОЧНИК ГОТОВ] Файлы игры извлечены в source_xml. Включите опцию обработки и запустите снова для создания XML ассетов.",
    },
    "ja": {
        "commentsRequireLanguage": "GUID コメント (-c) にはゲーム言語 (texts_*.xml) が必要です。",
        "sourceExtractionComplete": "[ソース準備完了] ゲームファイルを source_xml に抽出しました。処理オプションを有効にして再実行し、アセット XML を生成してください。",
    },
    "ko": {
        "commentsRequireLanguage": "GUID 주석(-c)에는 게임 언어(texts_*.xml)가 필요합니다.",
        "sourceExtractionComplete": "[소스 준비됨] 게임 파일을 source_xml에 추출했습니다. 처리 옵션을 켜고 다시 실행하여 에셋 XML을 생성하세요.",
    },
    "zh": {
        "commentsRequireLanguage": "GUID 注释 (-c) 需要游戏语言 (texts_*.xml)。",
        "sourceExtractionComplete": "[源已就绪] 游戏文件已提取到 source_xml。启用处理选项并再次运行以生成资产 XML。",
    },
    "tw": {
        "commentsRequireLanguage": "GUID 註解 (-c) 需要遊戲語言 (texts_*.xml)。",
        "sourceExtractionComplete": "[來源就緒] 遊戲檔案已提取至 source_xml。啟用處理選項並再次執行以產生資產 XML。",
    },
}


def sync_strings_locale(locale: str) -> tuple[int, int]:
    base_path = LANG_DIR / "Strings.json"
    locale_path = LANG_DIR / f"Strings.{locale}.json"

    base = json.loads(base_path.read_text(encoding="utf-8"))
    existing = json.loads(locale_path.read_text(encoding="utf-8"))

    # Preserve existing translations, then apply structural base + patches.
    merged = deep_merge(base, existing)
    merged = deep_merge(merged, PATCHES.get(locale, {}))
    prune_orphans(merged)

    base_keys = flatten_keys(base)
    merged_keys = flatten_keys(merged)
    missing = base_keys - merged_keys
    extra = merged_keys - base_keys
    if missing or extra:
        raise RuntimeError(f"{locale_path.name}: missing={sorted(missing)} extra={sorted(extra)}")

    locale_path.write_text(
        json.dumps(merged, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return len(base_keys), len(extra)


def sync_console_locale(locale: str) -> int:
    en_path = CONSOLE_DIR / "console_en.json"
    locale_path = CONSOLE_DIR / f"console_{locale}.json"
    en = json.loads(en_path.read_text(encoding="utf-8"))
    data = json.loads(locale_path.read_text(encoding="utf-8"))

    patch = CONSOLE_PATCHES.get(locale, {})
    for key, value in patch.items():
        data[key] = value

    missing = set(en) - set(data)
    if missing:
        for key in sorted(missing):
            data[key] = en[key]

    locale_path.write_text(
        json.dumps(dict(sorted(data.items())), ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return len(patch)


def main() -> None:
    print("Syncing UI strings...")
    for locale in LOCALES:
        count, extra = sync_strings_locale(locale)
        print(f"  Strings.{locale}.json — {count} keys OK")

    print("Syncing console messages...")
    for locale in LOCALES:
        added = sync_console_locale(locale)
        print(f"  console_{locale}.json — patched {added} keys")

    print("Done.")


if __name__ == "__main__":
    main()
