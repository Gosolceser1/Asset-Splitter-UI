import json, re
from pathlib import Path

console_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
pl = json.loads((console_dir / 'console_pl.json').read_text(encoding='utf-8'))

must_have_diacritics = {
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
    'zawierajace': 'zawierające', 'Uzyj': 'Użyj',
}

issues = []
for k in sorted(pl.keys()):
    v = pl[k]
    if not isinstance(v, str):
        continue
    words = re.findall(r'\b[A-Za-z]+\b', v)
    for w in words:
        if w in must_have_diacritics:
            issues.append((k, w, must_have_diacritics[w]))

if issues:
    print(f'Found {len(issues)} words missing diacritics:')
    for k, w, correct in issues:
        print(f'  {k}: "{w}" -> "{correct}"')
else:
    print('No missing diacritics found.')
