import json, re
from pathlib import Path

console_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
path = console_dir / 'console_pl.json'
pl = json.loads(path.read_text(encoding='utf-8'))

# Map of word -> correct form with diacritics
replacements = {
    'sie': 'się', 'wiecej': 'więcej', 'pelny': 'pełny', 'bledy': 'błędy',
    'blad': 'błąd', 'szczegol': 'szczegół', 'wlasnie': 'właśnie',
    'wyjscie': 'wyjście', 'tlumacz': 'tłumacz', 'wlacz': 'włącz',
    'wylacz': 'wyłącz', 'zmien': 'zmień', 'sprawdz': 'sprawdź',
    'otworz': 'otwórz', 'wyodrebn': 'wyodrębn', 'sciezka': 'ścieżka',
    'zakoncz': 'zakończ', 'odswiez': 'odśwież', 'Otworz': 'Otwórz',
    'Wlacz': 'Włącz', 'Zmien': 'Zmień', 'Wiecej': 'Więcej',
    'Pelny': 'Pełny', 'jesli': 'jeśli', 'zawierajacy': 'zawierający',
    'przegladania': 'przeglądania', 'szablonow': 'szablonów',
    'powyzej': 'powyżej', 'latke': 'łatkę', 'powyzszy': 'powyższy',
    'juz': 'już', 'polacz': 'połącz', 'mala': 'mała', 'zmiane': 'zmianę',
    'nazwe': 'nazwę', 'wyswietlanej': 'wyświetlanej', 'wedlug': 'według',
    'przegladac': 'przeglądać', 'zewnetrzny': 'zewnętrzny',
    'opakowujacy': 'opakowujący', 'wewnatrz': 'wewnątrz',
    'ktorych': 'których', 'udostepnieniem': 'udostępnieniem',
    'Uzyj': 'Użyj', 'uzyj': 'użyj', 'dopasowujac': 'dopasowując',
    'wartosci': 'wartości', 'Duzo': 'Dużo', 'niz': 'niż',
    'kolejnosci': 'kolejności', 'Obsluguje': 'Obsługuje',
    'udalo': 'udało', 'skopiowac': 'skopiować',
    'zawierajace': 'zawierające',
}

total_fixes = 0
for k, v in pl.items():
    if not isinstance(v, str):
        continue
    new_v = v
    for wrong, correct in replacements.items():
        # Use word boundaries to replace only whole words
        new_v = re.sub(r'\b' + re.escape(wrong) + r'\b', correct, new_v)
    if new_v != v:
        pl[k] = new_v
        total_fixes += 1

path.write_text(
    json.dumps(dict(sorted(pl.items())), ensure_ascii=False, indent=2) + '\n',
    encoding='utf-8'
)
print(f'Fixed diacritics in {total_fixes} strings')
