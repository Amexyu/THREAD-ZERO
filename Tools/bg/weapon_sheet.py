# 把 Gemini 出的「一张图里竖排几把武器」切成游戏用的武器部件（卡 27）。
# 用法：python3 weapon_sheet.py 原图.jpeg w10,w11,w12,w13,w14 w8,w6,w3,w5,w9 [--preview 预览.png]
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
cols = (al > .3).sum(0) > 0
runs = []; x = 0; W = len(cols)
while x < W:
    if cols[x]:
        s = x
        while x < W and cols[x]: x += 1
        if runs and s - runs[-1][1] < 12: runs[-1] = (runs[-1][0], x)
        else: runs.append((s, x))
    else: x += 1
runs = [r for r in runs if r[1] - r[0] > 20]
assert len(runs) == len(ids), ('切出来 %d 段，应该是 %d 把' % (len(runs), len(ids)), runs)
def despill(img, band=4):
    b = np.asarray(img).astype(int); alp = b[..., 3]
    near = np.asarray(Image.fromarray(((alp < 200) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(band * 2 + 1))) > 0
    m = np.clip(np.minimum(b[..., 0], b[..., 2]) - b[..., 1], 0, None) * near
    b[..., 0] -= m; b[..., 2] -= m
    return Image.fromarray(np.clip(b, 0, 255).astype(np.uint8), 'RGBA')
out = []
for (x0, x1), nid, old in zip(runs, ids, olds):
    rows = np.where((al[:, x0:x1] > .3).sum(1) > 0)[0]; y0, y1 = rows[0], rows[-1] + 1
    m = 6; crop = a[max(0, y0 - m):y1 + m, max(0, x0 - m):x1 + m]
    img = Image.fromarray(crop.clip(0, 255).astype(np.uint8), 'RGBA')
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
