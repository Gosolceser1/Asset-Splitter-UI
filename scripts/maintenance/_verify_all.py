import json, re
from pathlib import Path

base = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')
en = json.loads((base / 'console_en.json').read_text(encoding='utf-8'))

whitelist = {
    'bannerTopBorder','bannerSeparator','bannerBottomBorder','headerTopBorder','headerBottomBorder',
    'gameDetectionBanner','assetSplitVersion','enhancedEdition','done','addedCFlag','appliedTFlag',
    'applyingFolderOrganization','applyingRegionalIngredients','applyingTranslations','applyingXmlCleanup',
    'assetExtractionSuccess','issueSummarySampleBulletPrefix','issueSummarySampleFileDetail',
    'issueSummarySampleGuid','okWithMessage','modCategoryName','issueSummaryGroupLine',
}
debug_keys = {k for k in en if k.startswith('debug')}
readme_keys = {k for k in en if k.startswith('readme')}
help_keys = {k for k in en if k.startswith('help')}
skip = whitelist | debug_keys | readme_keys | help_keys

def placeholders(s):
    return sorted(re.findall(r'\{(\d+)\}', s))

print('=== Untranslated user-visible keys ===')
for locale in ['de','ru','es','fr','it','pl','ja','ko','zh','tw']:
    data = json.loads((base / f'console_{locale}.json').read_text(encoding='utf-8'))
    still = [k for k, v in data.items() if k in en and v == en[k] and k not in skip]
    print(f'{locale}: {len(still)}')

print()
print('=== Placeholder mismatches (non-readme) ===')
for locale in ['de','ru','es','fr','it','pl','ja','ko','zh','tw']:
    data = json.loads((base / f'console_{locale}.json').read_text(encoding='utf-8'))
    bad = [
        (k, placeholders(en[k]), placeholders(data[k]))
        for k in en
        if k in data and placeholders(en[k]) != placeholders(data[k]) and k not in readme_keys
    ]
    if bad:
        print(f'{locale}: {len(bad)} mismatches')
        for k, ep, lp in bad:
            print(f'  {k}: EN={ep} {locale}={lp}')
    else:
        print(f'{locale}: clean')
