import json
from pathlib import Path

console_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
pl = json.loads((console_dir / 'console_pl.json').read_text(encoding='utf-8'))

fixed = 0
for k, v in pl.items():
    if not isinstance(v, str):
        continue
    # Detect mojibake: try encoding as cp1252 and decoding as utf-8
    try:
        candidate = v.encode('cp1252').decode('utf-8')
        # Only apply if the candidate has Polish diacritics and the original didn't
        if candidate != v and any(c in candidate for c in 'łśćżóąęćńźŁŚĆŻÓĄĘĆŃŹ'):
            pl[k] = candidate
            fixed += 1
            print(f'  FIXED {k}: {repr(v[:60])} -> {repr(candidate[:60])}')
    except (UnicodeEncodeError, UnicodeDecodeError):
        pass

print(f'\nTotal mojibake fixes: {fixed}')

# Write back
path = console_dir / 'console_pl.json'
path.write_text(
    json.dumps(dict(sorted(pl.items())), ensure_ascii=False, indent=2) + '\n',
    encoding='utf-8'
)
print('File written.')
