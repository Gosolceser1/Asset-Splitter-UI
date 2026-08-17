import json
from pathlib import Path

console_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')

for locale in ['fr', 'ja', 'ko', 'zh', 'tw']:
    path = console_dir / f'console_{locale}.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    fixed = 0
    for k, v in data.items():
        if not isinstance(v, str):
            continue
        try:
            candidate = v.encode('cp1252').decode('utf-8')
            if candidate != v and any(ord(c) > 127 for c in candidate):
                data[k] = candidate
                fixed += 1
        except (UnicodeEncodeError, UnicodeDecodeError):
            pass
    if fixed > 0:
        path.write_text(
            json.dumps(dict(sorted(data.items())), ensure_ascii=False, indent=2) + '\n',
            encoding='utf-8'
        )
        print(f'{locale}: fixed {fixed} mojibake strings')
    else:
        print(f'{locale}: clean')
