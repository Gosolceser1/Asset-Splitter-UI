import json
from pathlib import Path

base = Path(r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\config\05_Console_Messages')

fixes = {
    'ko': {
        # Dropped {0} — restore it
        'compareTemplatesUnchanged': '[비교] 변경 없음 - {0}개 항목이 동기화되어 있습니다',
        'creatingOutputDirectory': '[INFO] 출력 디렉토리 생성 중: {0}',
        'inheritingTemplateProperties': '{0}개의 에셋에 대해 템플릿 속성 상속 중...',
        # Extra {0} added — remove it (no args in C# call)
        'compareTemplatesNewHeader': '[비교] 새 템플릿 (게임에만 있고 구성에 없음):',
        'compareTemplatesRemovedHeader': '[비교] 제거된 템플릿 (구성에만 있고 게임에 없음):',
        # Extra {1} added — only 1 arg in C# call
        'couldNotMoveToTemplateFolder': '[경고] 파일을 템플릿 폴더로 이동할 수 없습니다: {0}',
        # Only 2 placeholders — needs all 4
        'formattingRunSummary': '[포맷] 포맷 실행 완료: {0}개 파일 주석 처리됨 ({1}개 주석), {2}개 템플릿 이동, {3}개 Template 노드 누락',
        # Extra {1} — only 1 arg
        'gameBuildDetected': '[게임] 감지된 빌드: {0}',
    },
    'zh': {
        'compareTemplatesUnchanged': '[比较] 无变化 - {0} 项已同步',
        'creatingOutputDirectory': '[INFO] 正在创建输出目录: {0}',
        'inheritingTemplateProperties': '正在为 {0} 个资产继承模板属性...',
        'compareTemplatesNewHeader': '[比较] 新模板（在游戏中但不在配置中）:',
        'compareTemplatesRemovedHeader': '[比较] 已删除的模板（在配置中但不在游戏中）:',
        'couldNotMoveToTemplateFolder': '[警告] 无法将文件移动到模板文件夹: {0}',
        'formattingRunSummary': '[格式化] 格式化运行完成: {0} 个文件已注释（{1} 条注释），{2} 次模板移动，{3} 个缺少 Template 节点',
        'gameBuildDetected': '[游戏] 检测到版本: {0}',
    },
    'tw': {
        'compareTemplatesUnchanged': '[比較] 無變化 - {0} 個項目已同步',
        'creatingOutputDirectory': '[INFO] 正在建立輸出目錄: {0}',
        'inheritingTemplateProperties': '正在為 {0} 個資產繼承範本屬性...',
        'compareTemplatesNewHeader': '[比較] 新範本（在遊戲中但不在設定中）:',
        'compareTemplatesRemovedHeader': '[比較] 已移除的範本（在設定中但不在遊戲中）:',
        'couldNotMoveToTemplateFolder': '[警告] 無法將檔案移動到範本資料夾: {0}',
        'formattingRunSummary': '[格式化] 格式化執行完成: {0} 個檔案已加注釋（{1} 條注釋），{2} 次範本移動，{3} 個缺少 Template 節點',
        'gameBuildDetected': '[遊戲] 偵測到版本: {0}',
    },
}

for locale, changes in fixes.items():
    path = base / f'console_{locale}.json'
    data = json.loads(path.read_text(encoding='utf-8'))
    for k, v in changes.items():
        data[k] = v
    path.write_text(
        json.dumps(dict(sorted(data.items())), ensure_ascii=False, indent=2) + '\n',
        encoding='utf-8'
    )
    print(f'{locale}: applied {len(changes)} placeholder fixes')
