# 把 Gemini 出的「一张图里竖排几把武器」切成游戏用的武器部件（卡 27）。
# 用法：python3 weapon_sheet.py 原图.jpeg w10,w11,w12,w13,w14 w8,w6,w3,w5,w9 [--preview 预览.png]
#   新 id 写 - 表示这一把跳过（画错了另外重画的）；id 后面加 ^ 表示这一把画反了（握把在下），上下翻过来再用。锁链类（借 w7）切完再用 weapon_hook.py 把钩头单独切出来。
#   第二组是每把新武器「借用的旧武器」（同类招式）：新图按旧武器的高度缩放，握点 / 尖端按旧武器的上下比例放，
#   横向位置取那一高度上实际画出来的部分的中心；weapon_class 跟旧武器一样（动作、招式不变）。
#   写到 ThreadZero/Assets/Resources/Art/V13/Parts/<新 id>/weapon.png + part.json。原图不动。
import sys, os, json, numpy as np
from PIL import Image, ImageFilter, ImageDraw
def key(a):   # 同 bg_lib.key：按品红程度抠透明，并把边缘的品红色偏减掉
    r, g, b = a[..., 0], a[..., 1], a[..., 2]; score = np.minimum(r, b) - g; alpha = np.clip((190 - score) / 110, 0, 1)
    ex = np.clip(score, 0, None); k = np.clip((score - 8) / 40, 0, 1)
    return np.dstack([r - ex * k, g, b - ex * k, alpha * 255]).clip(0, 255)
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
PARTS = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/Parts')
args = sys.argv[1:]; preview = None
if '--preview' in args: k = args.index('--preview'); preview = args[k + 1]; del args[k:k + 2]
src, ids, olds = args[0], args[1].split(','), args[2].split(',')
a = key(np.asarray(Image.open(src).convert('RGB')).astype(np.float32))
al = a[..., 3] / 255
# 按连通块切（不按列投影：重锤的锤头和旁边钩索的爪子在横向上会挨在一起）。先 4 倍缩小找块，再放大回原图当遮罩。
def components(mask, k=4):
    H, W = mask.shape; m = mask[:H // k * k, :W // k * k].reshape(H // k, k, W // k, k).max(axis=(1, 3))
    lab = np.zeros(m.shape, int); n = 0; sizes = []
    for y in range(m.shape[0]):
        for x in np.nonzero(m[y] & (lab[y] == 0))[0]:
            if lab[y, x]: continue
            n += 1; stack = [(y, x)]; lab[y, x] = n; cnt = 0
            while stack:
                cy, cx = stack.pop(); cnt += 1
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if 0 <= ny < m.shape[0] and 0 <= nx < m.shape[1] and m[ny, nx] and not lab[ny, nx]:
                            lab[ny, nx] = n; stack.append((ny, nx))
            sizes.append(cnt)
    return lab, sizes, k
lab, sizes, K = components(al > .3)
big = [i + 1 for i, c in enumerate(sizes) if c >= .12 * max(sizes)]
# Gemini 有时会在旁边留一个半透明的「残影」（参考图里的旧武器淡淡地印在背景上）：实心像素太少的块不要
def solid(i):
    m = np.kron((lab == i).astype(np.uint8), np.ones((K, K), np.uint8)).astype(bool)
    v = al[:m.shape[0], :m.shape[1]][m]; return (v > .9).mean() if v.size else 0
ghosts = [i for i in big if solid(i) < .3]
if ghosts: print('去掉半透明残影', len(ghosts), '块'); big = [i for i in big if i not in ghosts]
def cx(i): ys, xs = np.nonzero(lab == i); return xs.mean()
big.sort(key=cx)
# 同一把武器被切成两块（比如缠在锤柄外面的冷却管只在两头连着）：横向范围大半重叠的块合成一块
def xr(i): xs = np.nonzero(lab == i)[1]; return xs.min(), xs.max()
merged = True
while merged and len(big) > len(ids):
    merged = False
    for j in range(len(big) - 1):
        (a0, a1), (b0, b1) = xr(big[j]), xr(big[j + 1])
        over = min(a1, b1) - max(a0, b0)
        if over > .5 * min(a1 - a0, b1 - b0):
            lab[lab == big[j + 1]] = big[j]; del big[j + 1]; merged = True; break
assert len(big) == len(ids), ('切出来 %d 块，应该是 %d 把' % (len(big), len(ids)), [sizes[i - 1] for i in big])
def full_mask(i):
    m = np.kron((lab == i).astype(np.uint8), np.ones((K, K), np.uint8)).astype(bool)
    out = np.zeros(al.shape, bool); out[:m.shape[0], :m.shape[1]] = m; return out
runs = []
for i in big:
    fm = full_mask(i); ys, xs = np.nonzero(fm); runs.append((xs.min(), xs.max() + 1, ys.min(), ys.max() + 1, fm))
def despill(img, band=4):
    b = np.asarray(img).astype(int); alp = b[..., 3]
    near = np.asarray(Image.fromarray(((alp < 200) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(band * 2 + 1))) > 0
    m = np.clip(np.minimum(b[..., 0], b[..., 2]) - b[..., 1], 0, None) * near
    b[..., 0] -= m; b[..., 2] -= m
    return Image.fromarray(np.clip(b, 0, 255).astype(np.uint8), 'RGBA')
out = []
for (x0, x1, y0, y1, fm), nid, old in zip(runs, ids, olds):
    if nid == '-': continue   # 这一把不要（比如画错了另外重画）
    m = 6; crop = (a * fm[..., None])[max(0, y0 - m):y1 + m, max(0, x0 - m):x1 + m]   # 别的块的像素清掉
    img = Image.fromarray(crop.clip(0, 255).astype(np.uint8), 'RGBA')
    if nid.endswith('^'):   # 画反了（枪口朝上）：上下翻过来（不是转 180°，那样握把会跑到另一边）
        nid = nid[:-1]; img = img.transpose(Image.FLIP_TOP_BOTTOM)
    oj = json.load(open(os.path.join(PARTS, old, 'part.json'))); oim = Image.open(os.path.join(PARTS, old, 'weapon.png'))
    sc = oj.get('scale', 4); H = oim.height
    w = max(4, round(img.width * H / img.height))
    pm = img.convert('RGBa').resize((w, H), Image.LANCZOS).convert('RGBA')   # 预乘 alpha 缩放，边缘不发黑
    pm = despill(pm)
    pa = np.asarray(pm)[..., 3] / 255
    def at(ratio):
        y = int(round(ratio * H)); band = pa[max(0, y - max(2, H // 25)):y + max(3, H // 25)]
        xs = np.arange(w); wsum = band.sum(0)
        return (float((xs * wsum).sum() / max(1e-6, wsum.sum())), float(y))
    op, ot = oj['weapon']['pivot'], oj['weapon'].get('tip', [oj['weapon']['pivot'][0], oim.height / sc])
    gx, gy = at(op[1] * sc / oim.height); tx, ty = at(ot[1] * sc / oim.height)
    d = os.path.join(PARTS, nid); os.makedirs(d, exist_ok=True)
    pm.save(os.path.join(d, 'weapon.png'), optimize=True)
    pj = {'weapon_class': oj.get('weapon_class'), 'weapon': {'file': 'weapon.png', 'pivot': [round(gx / sc, 2), round(gy / sc, 2)], 'tip': [round(tx / sc, 2), round(ty / sc, 2)]}, 'scale': sc}
    json.dump(pj, open(os.path.join(d, 'part.json'), 'w'), ensure_ascii=False)
    out.append((nid, old, pm, (gx, gy), (tx, ty), oim))
    print(nid, '借', old, pj['weapon_class'], pm.size, 'pivot', pj['weapon']['pivot'], 'tip', pj['weapon']['tip'])
if preview:
    S = 2; pad = 30; Wt = sum(max(o[2].width, o[5].width) * S + pad for o in out) + pad; Ht = max(max(o[2].height, o[5].height) for o in out) * S * 2 + pad * 3
    sheet = Image.new('RGBA', (Wt, Ht), (58, 60, 64, 255)); dr = ImageDraw.Draw(sheet); x = pad
    for nid, old, pm, g, t, oim in out:
        big = pm.resize((pm.width * S, pm.height * S), Image.NEAREST); sheet.alpha_composite(big, (x, pad))
        for (px, py), c in ((g, (60, 230, 90, 255)), (t, (255, 70, 60, 255))): dr.ellipse((x + px * S - 6, pad + py * S - 6, x + px * S + 6, pad + py * S + 6), outline=c, width=3)
        ob = oim.convert('RGBA').resize((oim.width * S, oim.height * S), Image.NEAREST); sheet.alpha_composite(ob, (x, pad * 2 + Ht // 2 - pad))
        x += max(pm.width, oim.width) * S + pad
    sheet.convert('RGB').save(preview); print('preview', preview)
