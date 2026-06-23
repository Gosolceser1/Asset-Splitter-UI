import json, re
from pathlib import Path

lang_dir = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\Localization\Languages')
base = json.loads((lang_dir / 'Strings.json').read_text(encoding='utf-8'))

def flatten(d, prefix=''):
    out = {}
    for k, v in d.items():
        key = f'{prefix}.{k}' if prefix else k
        if isinstance(v, dict):
            out.update(flatten(v, key))
        else:
            out[key] = v
    return out

def placeholders(s):
    return sorted(re.findall(r'\{(\d+)\}', s))

base_flat = flatten(base)

# Keys that are legitimately identical to English
whitelist = {
    'gameNames.anno1800', 'gameNames.anno117', 'gameNames.anno117Demo',
    'app.title', 'app.windowTitle',
    'labels.singleGuid', 'labels.outputDirectory',
    'checkboxes.modOpsWrap',
    'consoleMessages.headerTopBorder', 'consoleMessages.headerBottomBorder',
    'consoleMessages.bannerTopBorder', 'consoleMessages.bannerBottomBorder',
    'consoleMessages.bannerSeparator', 'consoleMessages.gameDetectionBanner',
    'consoleMessages.consoleHeaderCenter',
    'issueSummary.groupHeader', 'issueSummary.sampleBullet',
    'issueSummary.sampleGuid', 'issueSummary.sampleFileDetail',
    # technical/format strings
    'consoleMessages.assetSplitVersion', 'consoleMessages.enhancedEdition',
    'consoleMessages.helpSyntax', 'consoleMessages.syntaxLabel',
    'consoleMessages.done', 'consoleMessages.optionOff',
}

locales = ['de', 'es', 'fr', 'it', 'pl', 'ru', 'ja', 'ko', 'zh', 'tw']

for locale in locales:
    path = lang_dir / f'Strings.{locale}.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    data_flat = flatten(data)

    untranslated = [k for k, v in data_flat.items()
                    if k in base_flat and v == base_flat[k] and k not in whitelist]

    ph_mismatches = [(k, placeholders(base_flat[k]), placeholders(v))
                     for k, v in data_flat.items()
                     if k in base_flat and isinstance(v, str)
                     and placeholders(base_flat[k]) != placeholders(v)]

    missing = [k for k in base_flat if k not in data_flat]
    extra = [k for k in data_flat if k not in base_flat]

    issues = []
    if untranslated:
        issues.append(f'  UNTRANSLATED ({len(untranslated)}): {untranslated}')
    if ph_mismatches:
        issues.append(f'  PLACEHOLDER MISMATCHES ({len(ph_mismatches)}):')
        for k, ep, lp in ph_mismatches:
            issues.append(f'    {k}: EN={ep} {locale}={lp}')
    if missing:
        issues.append(f'  MISSING KEYS ({len(missing)}): {missing[:10]}')
    if extra:
        issues.append(f'  EXTRA KEYS ({len(extra)}): {extra[:10]}')

    if issues:
        print(f'\n=== {locale} ===')
        for i in issues:
            print(i)
    else:
        print(f'{locale}: OK')
