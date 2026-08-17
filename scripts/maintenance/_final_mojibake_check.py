import json, re
from pathlib import Path

console_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
lang_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\Localization\Languages')

# Refined mojibake detection: try cp1252->utf-8 round-trip
def has_mojibake(v):
    if not isinstance(v, str):
        return False
    try:
        candidate = v.encode('cp1252').decode('utf-8')
        return candidate != v and any(ord(c) > 127 for c in candidate)
    except (UnicodeEncodeError, UnicodeDecodeError):
        return False

print('=== Console files mojibake check ===')
for locale in ['de','es','fr','it','pl','ru','ja','ko','zh','tw']:
    data = json.loads((console_dir / f'console_{locale}.json').read_text(encoding='utf-8'))
    count = sum(1 for v in data.values() if has_mojibake(v))
    print(f'{locale}: {"clean" if count == 0 else f"{count} MOJIBAKE"}')

print('\n=== UI Strings files mojibake check ===')
for locale in ['de','es','fr','it','pl','ru','ja','ko','zh','tw']:
    data = json.loads((lang_dir / f'Strings.{locale}.json').read_text(encoding='utf-8'))
    count = sum(1 for v in data.values() if has_mojibake(v))
    print(f'{locale}: {"clean" if count == 0 else f"{count} MOJIBAKE"}')
