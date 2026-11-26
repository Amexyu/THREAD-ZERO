# 卡40：比例修正（11-26 用户：「你看这比例对吗？」）。在 部件/ 的基础上：身体收窄、卫衣略短、短裤变短；
# 腿的条纹袜段拉长；袖子略短略细；拳头缩小。输出到 部件v3/，parts.json 里的点位跟着换算。
import os, json, numpy as np
from PIL import Image
SRC, OUT = '部件', '部件v3'; os.makedirs(OUT, exist_ok=True)
m = json.load(open(os.path.join(SRC, 'parts.json'))); SC = m['scale']
def load(n): return Image.open(os.path.join(SRC, n + '.png')).convert('RGBA')
def vbands(img, bands):   # bands: [(y0, y1, k)] 覆盖整张图，逐段竖向缩放后拼起来；返回新图和换算函数
    W = img.width; pieces = []; maps = []; ny = 0
    for y0, y1, k in bands:
        h = max(1, round((y1 - y0) * k)); pieces.append(img.crop((0, y0, W, y1)).resize((W, h), Image.LANCZOS)); maps.append((y0, y1, ny, k)); ny += h
    out = Image.new('RGBA', (W, ny)); y = 0
    for p in pieces: out.alpha_composite(p, (0, y)); y += p.height
    def f(yy):
        for y0, y1, n0, k in maps:
            if yy <= y1: return n0 + (yy - y0) * k
        return ny
    return out, f
P = m['parts']; rig = m['rig']
# ---- 身体：卫衣 ×0.94，短裤 ×0.62，左右 ×0.84 ----
t = load('torso'); H = t.height; SH = round(H * 256 / 343)   # 短裤从这一行开始（原图量的 465 → 缩放后）
hipY = P['torso']['pivot'][1] * SC; hipX = P['torso']['pivot'][0] * SC
t2, fy = vbands(t, [(0, SH, .94), (SH, H, .62)]); KX = .84
t2 = t2.resize((round(t2.width * KX), t2.height), Image.LANCZOS); t2.save(os.path.join(OUT, 'torso.png'))
nhipY = fy(hipY); nhipX = hipX * KX
def rel(off):   # 原来相对胯部的点 → 新的相对胯部的点（单位）
    ay = hipY + off[1] * SC; ax = hipX + off[0] * SC
    return [round((ax * KX - nhipX) / SC, 2), round((fy(ay) - nhipY) / SC, 2)]
P['torso']['pivot'] = [round(nhipX / SC, 2), round(nhipY / SC, 2)]
rig['head'] = rel(rig['head']); rig['pack'] = rel(rig['pack'])
m['shoulder'] = rel(m['shoulder']); m['shoulderF'] = rel(m['shoulderF'])
rig['leg_near'] = [round(rig['leg_near'][0] * KX, 2), 0]; rig['leg_far'] = [round(rig['leg_far'][0] * KX, 2), 0]
torsoHem = t2.height
# ---- 腿：短裤 ×0.62，条纹袜 ×1.8，鞋不动 ----
legLen = 0
for n in ('leg_near', 'leg_far'):
    l = load(n); H = l.height; k0 = round(H * 33 / 323); hem = round(H * 144 / 323); shoe = round(H * 226 / 323)
    l2, gy = vbands(l, [(0, k0, 1), (k0, hem, .62), (hem, shoe, 1.8), (shoe, H, 1)]); l2.save(os.path.join(OUT, n + '.png'))
    nhem = gy(hem); py = nhem - (torsoHem - nhipY)
    P[n]['pivot'] = [P[n]['pivot'][0], round(py / SC, 2)]
    ys = np.nonzero((np.asarray(l2)[..., 3] > 128).any(1))[0]; legLen = max(legLen, (ys.max() - py) / SC)
rig['hip'] = [rig['hip'][0], -round(legLen, 2)]
# ---- 袖子：竖 ×0.88，横 ×0.9 ----
for n in ('arm', 'arm_far'):
    a = load(n); a2 = a.resize((round(a.width * .9), round(a.height * .88)), Image.LANCZOS); a2.save(os.path.join(OUT, n + '.png'))
    P[n]['pivot'] = [round(P[n]['pivot'][0] * .9, 2), round(P[n]['pivot'][1] * .88, 2)]; P[n]['hand'] = [round(P[n]['hand'][0] * .9, 2), round(P[n]['hand'][1] * .88, 2)]
# ---- 拳头 ×0.7 ----
for n in ('hand', 'hand_far'):
    a = load(n); a.resize((round(a.width * .7), round(a.height * .7)), Image.LANCZOS).save(os.path.join(OUT, n + '.png'))
    P[n]['pivot'] = [round(P[n]['pivot'][0] * .7, 2), round(P[n]['pivot'][1] * .7, 2)]
for n in ('head', 'pack', 'weapon'):
    if os.path.exists(os.path.join(SRC, n + '.png')): load(n).save(os.path.join(OUT, n + '.png'))
json.dump(m, open(os.path.join(OUT, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
print(json.dumps({k: m[k] for k in ('shoulder', 'shoulderF', 'rig')}, ensure_ascii=False)); print(P['torso'], P['leg_near'], P['arm'], P['hand'])
