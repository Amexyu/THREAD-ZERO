#!/usr/bin/env python3
"""v0.21 四语：把 Tools/loc/master/*.txt（一行一条：中文§日文§英文§韩文；re: 开头是正则模板）
生成 ThreadZero/Assets/Resources/Loc/{ja,en,ko}.txt（Loc.cs 读的格式：中文<Tab>译文）。
同时检查：重复的 key、列数不对、代码里还有哪些中文字面量没有整句条目（片段兜底也许能翻，列出来供人工看）。
用法：python3 Tools/loc/build_loc.py [--missing]"""
import glob, os, re, sys
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'ThreadZero', 'Assets', 'Resources', 'Loc')
LANGS = ['ja', 'en', 'ko']
rows, keys, problems = [], {}, []
for path in sorted(glob.glob(os.path.join(os.path.dirname(__file__), 'master', '*.txt'))):
    for n, line in enumerate(open(path, encoding='utf-8'), 1):
        line = line.rstrip('\n')
        if not line.strip() or line.startswith('#'): continue
        parts = line.split('§')
        if len(parts) != 4: problems.append(f'{os.path.basename(path)}:{n} 列数 {len(parts)}：{line[:40]}'); continue
        k = parts[0] if parts[0].startswith('re:') else parts[0].strip()
        if k in keys:
            if keys[k] != parts[1:]: problems.append(f'{os.path.basename(path)}:{n} 重复且译文不同：{k}')
            continue
        if k.startswith('re:'):
            try: re.compile('^' + k[3:] + '$')
            except re.error as e: problems.append(f'{os.path.basename(path)}:{n} 正则写错：{k} {e}'); continue
        keys[k] = parts[1:]; rows.append((k, parts[1:]))
os.makedirs(OUT, exist_ok=True)
for i, lang in enumerate(LANGS):
    with open(os.path.join(OUT, lang + '.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# 自动生成，别直接改：改 Tools/loc/master/*.txt 后运行 python3 Tools/loc/build_loc.py\n')
        f.write('# 一行一条「中文<Tab>译文」；re: 开头的是正则模板，{1} {2}… 是括号抓到的部分（会再翻译一遍）\n')
        for k, v in sorted(rows, key=lambda r: (not r[0].startswith('re:'), r[0])):
            # 模板放前面（Loc 按顺序匹配）；更具体的模板在 master 里写在前面
            pass
        for k, v in [r for r in rows if r[0].startswith('re:')] + sorted([r for r in rows if not r[0].startswith('re:')]):
            val = v[i].replace('\t', ' ')
            f.write(k.replace('\t', ' ') + '\t' + val + '\n')
print(f'条目 {len(rows)}（模板 {sum(1 for k, _ in rows if k.startswith("re:"))}）→ {OUT}')
for p in problems: print('  ⚠', p)
if '--missing' in sys.argv:
    lit = re.compile(r'"((?:[^"\\]|\\.)*)"'); han = re.compile(r'[一-鿿]')
    seen = {}
    for p in sorted(glob.glob(os.path.join(ROOT, 'ThreadZero', 'Assets', 'Scripts', '*.cs'))):
        for ln, line in enumerate(open(p, encoding='utf-8'), 1):
            s = line.strip()
            if s.startswith('//') or 'Debug.Log' in line: continue
            for m in lit.finditer(line):
                t = m.group(1).replace('\\"', '"').strip()
                if han.search(t) and t not in keys: seen.setdefault(t, f'{os.path.basename(p)}:{ln}')
    print(f'代码里还没有整句条目的中文字面量：{len(seen)}（拼接片段多数靠模板 / 片段兜底，下面只列前 80 个）')
    for t, w in list(seen.items())[:80]: print('  ', w, '|', t.replace('\n', '\\n'))
