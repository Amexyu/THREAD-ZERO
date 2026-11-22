# v0.21 第三～五章防具：一张图出 5 件（16:9，五格），每格里垫着初始部件当位置 / 大小的模板，出图后按固定格子切回部件坐标。
#   做模板：python3 armor_sheet.py template <部件> <输出.png>          部件 = head torso arm arm_far pack leg_near leg_far
#   切图：  python3 armor_sheet.py cut <部件> <出图> <id1> … <id5> [--adj i:dx,dy,s …] [--preview 文件]
#           id 用 - 跳过；--adj 第 i 格（0 起）整体挪 dx,dy（部件像素）再缩放 s。写进 Resources/Art/V13/Parts/<id>/，合并 part.json。
#   图标：  python3 armor_sheet.py icons <出图> <id1> … <id5>          头部单品图（不带人头），按空白缝切开，各存 384×384 的 icon.png
import sys, os, json, numpy as np
from PIL import Image
ROOT = os.path.expanduser('~/mnt/THREAD-ZERO/'); ART = ROOT + 'ThreadZero/Assets/Resources/Art/V13/'
W, H, CW = 2752, 1536, 550
#        左 上 右 下 留白（部件像素）, 放大
SPEC = {'head': (60, 70, 60, 50, 1.18), 'torso': (60, 30, 60, 130, 1.5), 'arm': (40, 30, 40, 50, 2.5), 'arm_far': (30, 30, 30, 30, 3.0),
        'pack': (60, 50, 40, 50, 2.2), 'leg_near': (20, 20, 20, 20, 3.0), 'leg_far': (20, 20, 20, 20, 3.0)}
M = json.load(open(ART + 'Starter/parts.json'))
def starter(part): return Image.open(ART + 'Starter/' + M['parts'][part]['file']).convert('RGBA')
def cell_rect(part, i):
    l, t, r, b, k = SPEC[part]; im = starter(part); cw, ch = (im.width + l + r) * k, (im.height + t + b) * k
    x0 = 1 + i * CW + (CW - cw) / 2; y0 = (H - ch) / 2
    return x0, y0, cw, ch
def template(part, out):
    l, t, r, b, k = SPEC[part]; im = starter(part)
    sheet = Image.new('RGBA', (W, H), (255, 0, 255, 255))
    big = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
    for i in range(5):
        x0, y0, cw, ch = cell_rect(part, i); sheet.alpha_composite(big, (round(x0 + l * k), round(y0 + t * k)))
    sheet.convert('RGB').save(out); print(out)
def key(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]; score = np.minimum(r, b) - g
    alpha = np.clip((190 - score) / 110, 0, 1); ex = np.clip(score, 0, None); k = np.clip((score - 8) / 40, 0, 1)
    return np.dstack([r - ex * k, g, b - ex * k, alpha * 255]).clip(0, 255).astype(np.uint8)
def components(m):
    """连通块（先膨胀 2 像素把细缝连上）。返回 标号图、每块的 (行切片, 列切片)、每块像素数。"""
    from PIL import ImageFilter
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5))) > 0
    h, w = m.shape; lab = np.zeros((h, w), np.int32); objs, sizes = [], []; n = 0
    for y0 in range(h):
        for x0 in np.nonzero(m[y0] & (lab[y0] == 0))[0]:
            if lab[y0, x0]: continue
            n += 1; st = [(y0, x0)]; lab[y0, x0] = n; cnt = 0; ys = [y0, y0]; xs = [x0, x0]
            while st:
                y, x = st.pop(); cnt += 1
                ys[0] = min(ys[0], y); ys[1] = max(ys[1], y); xs[0] = min(xs[0], x); xs[1] = max(xs[1], x)
                for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                    if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not lab[yy, xx]: lab[yy, xx] = n; st.append((yy, xx))
            objs.append((slice(ys[0], ys[1] + 1), slice(xs[0], xs[1] + 1))); sizes.append(cnt)
    return lab, objs, sizes
def merge_json(pid, upd):
    d = ART + 'Parts/' + pid; os.makedirs(d, exist_ok=True); p = d + '/part.json'
    j = json.load(open(p)) if os.path.exists(p) else {'scale': 4}
    j.update(upd); json.dump(j, open(p, 'w'), indent=1)
def cut(part, src, ids, adj, preview):
    l, t, r, b, k = SPEC[part]; st = starter(part); piv = M['parts'][part]['pivot']
    im = Image.open(src).convert('RGB'); sx, sy = im.width / W, im.height / H
    full = key(np.asarray(im).astype(np.float32))
    lab, objs, sizes = components(full[::4, ::4, 3] > 40)
    tiles = []
    for i, pid in enumerate(ids):
        if pid == '-': continue
        x0, y0, cw, ch = cell_rect(part, i); dx, dy, s = adj.get(i, (0, 0, 1))
        # 这一格的东西 = 整张图里中心落在这一格的那些连通块（东西可以伸出格子，邻格伸过来的碎片不要）
        cx0, cx1 = (1 + i * CW) * sx, (1 + (i + 1) * CW) * sx
        a = full.copy(); keep = np.zeros(lab.shape, bool)
        for li, sl in enumerate(objs, 1):
            if sl is None: continue
            mx = (sl[1].start + sl[1].stop) / 2 * 4
            if cx0 <= mx < cx1 and sizes[li - 1] > 30: keep |= lab == li
        keep = np.kron(keep, np.ones((4, 4), bool))[:a.shape[0], :a.shape[1]]
        a[..., 3] = np.where(keep, a[..., 3], 0)
        # 坐标系仍按格子：裁出 [格左, 格右] 加上伸出去的部分
        ys_, xs_ = np.nonzero(a[..., 3] > 40)
        lx = min(int(cx0), xs_.min()) if len(xs_) else int(cx0); rx = max(int(cx1), xs_.max() + 1) if len(xs_) else int(cx1)
        rgba = Image.fromarray(a[:, lx:rx], 'RGBA'); shift = (cx0 - lx)
        f = 1 / (k * sx) * s; small = rgba.resize((max(1, round(rgba.width * f)), max(1, round(rgba.height * f))), Image.LANCZOS)
        m = np.asarray(small)[..., 3] > 40
        if not m.any(): print('空格', i, pid); continue
        ys, xs = np.nonzero(m); bx0, by0, bx1, by1 = max(0, xs.min() - 8), max(0, ys.min() - 8), min(small.width, xs.max() + 9), min(small.height, ys.max() + 9)
        out = small.crop((bx0, by0, bx1, by1))
        # 初始部件左上角在缩小后格子图里的位置
        ox = ((x0 + l * k) - (1 + i * CW) + shift / sx) / k * s + dx; oy = (y0 + t * k) / k * s * (sy / sx) + dy
        pivot = [round(piv[0] + (ox - bx0) / 4, 2), round(piv[1] + (oy - by0) / 4, 2)]
        d = ART + 'Parts/' + pid; os.makedirs(d, exist_ok=True); out.save(d + '/' + part + '.png')
        entry = {'file': part + '.png', 'pivot': pivot}
        for extra in ('hand',):
            if extra in M['parts'][part]: entry[extra] = M['parts'][part][extra]
        upd = {part: entry}
        if part == 'torso': upd.update({'shoulder': M['shoulder'], 'shoulderF': M['shoulderF']})
        merge_json(pid, upd); print(pid, part, out.size, 'pivot', pivot)
        # 预览：新部件（按枢轴）叠在半透明的初始部件上
        pv = Image.new('RGBA', (out.width + 80, out.height + 80), (70, 72, 90, 255)); pv.alpha_composite(out, (40, 40))
        ghost = st.copy(); ghost.putalpha(ghost.getchannel('A').point(lambda v: v * 0.35))
        gx, gy = round(40 + pivot[0] * 4 - piv[0] * 4), round(40 + pivot[1] * 4 - piv[1] * 4)
        if 0 <= gx and 0 <= gy and gx + ghost.width <= pv.width and gy + ghost.height <= pv.height: pv.alpha_composite(ghost, (gx, gy))
        tiles.append(pv.convert('RGB'))
    if preview and tiles:
        sh = Image.new('RGB', (sum(t.width for t in tiles) + 10 * len(tiles), max(t.height for t in tiles)), (20, 20, 20)); x = 0
        for t_ in tiles: sh.paste(t_, (x, 0)); x += t_.width + 10
        sh.save(preview, quality=88)
def icons(src, ids):
    im = Image.open(src).convert('RGB'); a = key(np.asarray(im).astype(np.float32))
    cols = (a[..., 3] > 40).sum(0) > 2; spans = []; x = 0; Wd = len(cols)
    while x < Wd:
        if cols[x]:
            s0 = x
            while x < Wd and (cols[x] or cols[x:x + 24].any()): x += 1
            if x - s0 > 60: spans.append((s0, x))
        x += 1
    print('找到', len(spans), '件'); assert len(spans) == len(ids), '件数对不上'
    for (s0, s1), pid in zip(spans, ids):
        if pid == '-': continue
        sub = a[:, s0:s1]; ys = np.nonzero((sub[..., 3] > 40).any(1))[0]; it = Image.fromarray(sub[ys.min():ys.max() + 1], 'RGBA')
        f = 344 / max(it.size); it = it.resize((round(it.width * f), round(it.height * f)), Image.LANCZOS)
        ic = Image.new('RGBA', (384, 384), (0, 0, 0, 0)); ic.alpha_composite(it, ((384 - it.width) // 2, (384 - it.height) // 2))
        d = ART + 'Parts/' + pid; os.makedirs(d, exist_ok=True); ic.save(d + '/icon.png'); print(pid, 'icon')
if __name__ == '__main__':
    a = sys.argv
    if a[1] == 'template': template(a[2], a[3])
    elif a[1] == 'icons': icons(a[2], a[3:8])
    else:
        rest = a[9:]; adj = {}; preview = None
        while rest:
            if rest[0] == '--adj': i, v = rest[1].split(':'); adj[int(i)] = tuple(float(x) for x in v.split(',')); rest = rest[2:]
            elif rest[0] == '--preview': preview = rest[1]; rest = rest[2:]
            else: rest = rest[1:]
        cut(a[2], a[3], a[4:9], adj, preview)
