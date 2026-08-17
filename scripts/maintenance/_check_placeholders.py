import json
from pathlib import Path

base = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
en = json.loads((base / 'console_en.json').read_text(encoding='utf-8'))

keys = [
    'compareTemplatesUnchanged', 'compareTemplatesNewHeader', 'compareTemplatesRemovedHeader',
    'creatingOutputDirectory', 'couldNotMoveToTemplateFolder', 'inheritingTemplateProperties',
    'formattingRunSummary', 'gameBuildDetected',
]

print('=== ENGLISH ===')
for k in keys:
    print(f'  {k}: {repr(en[k])}')

for locale in ['ko', 'zh', 'tw']:
    data = json.loads((base / f'console_{locale}.json').read_text(encoding='utf-8'))
    print(f'\n=== {locale} ===')
    for k in keys:
        val = data.get(k, 'MISSING')
        print(f'  {k}: {repr(val)}')
