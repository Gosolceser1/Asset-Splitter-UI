path = r'c:\Users\vadim\OneDrive\Documents\Asset Splitter UI\src\AssetSplitter\ModReadmeWriter.cs'
with open(path, encoding='utf-8') as f:
    lines = f.readlines()
for i in range(499, 516):
    print(f'{i+1:03d} [{len(lines[i].rstrip(chr(10)))}] >{lines[i].rstrip(chr(10))}<')
