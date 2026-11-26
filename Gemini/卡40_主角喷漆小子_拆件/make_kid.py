# 卡40：把切好的喷漆小子部件（切图v2）做成游戏骨骼用的部件 + parts.json（Resources/Art/V17/Kid/ 的格式）。
# 原图量出来的点都写在 SRC 里（原图像素，部件自己的坐标）；输出统一缩到 SCALE 像素 = 1 个骨骼单位。
import os, sys, json, numpy as np
from PIL import Image
CUT = '切图v2'; OUT = sys.argv[1] if len(sys.argv) > 1 else '部件'
os.makedirs(OUT, exist_ok=True)
allim = np.asarray(Image.open(os.path.join(CUT, '_all.png'))); lab = np.load(os.path.join(CUT, '_lab.npy'))
comps = {c['i']: c for c in json.load(open(os.path.join(CUT, '_comps.json')))}
IDS = dict(head=1, torso=3, pack=2, arm=6, arm_far=7, hand=8, hand_far=9, leg_near=4, leg_far=5)
SRC_PER_UNIT = 9.08          # 原图 1271 像素高的人 ≈ 140 单位
SCALE = 5.0                  # 游戏里 1 单位 = 5 贴图像素
K = SCALE / SRC_PER_UNIT
def part(name, pad=8):
    c = comps[IDS[name]]; x0, y0, x1, y1 = c['x'] - pad, c['y'] - pad, c['x'] + c['w'] + pad, c['y'] + c['h'] + pad
    rgba = allim[y0:y1, x0:x1].copy(); m = lab[y0:y1, x0:x1] == IDS[name]
    rgba[..., 3] = np.where(m, rgba[..., 3], 0)
    return rgba
def opaque_center(a, y):   # 第 y 行不透明像素的横向中心
    xs = np.nonzero(a[int(y), :, 3] > 128)[0]; return (xs.min() + xs.max()) / 2 if len(xs) else a.shape[1] / 2
def rows(a): ys = np.nonzero((a[..., 3] > 128).any(1))[0]; return ys.min(), ys.max()
P = {}
# ---- 头：从下巴下面一点切掉脖子（脖子用身体上那一截）----
h = part('head'); t0, t1 = rows(h)
CUT_Y = t0 + 372; h[CUT_Y:, :, 3] = 0
P['head'] = dict(img=h, pivot=(opaque_center(h, CUT_Y - 6), CUT_Y - 8))
# ---- 身体 ----
t = part('torso'); a0, a1 = rows(t)
neck_x = opaque_center(t, a0 + 12)
ys = range(a0 + 110, a0 + 180); lefts = []; rights = []
for y in ys:
    xs = np.nonzero(t[y, :, 3] > 128)[0]; lefts.append(xs.min()); rights.append(xs.max())
L, R = min(lefts), max(rights)
hem = a1; hip_y = a0 + 470
xs = np.nonzero(t[hem - 20, :, 3] > 128)[0]; s0, s1 = xs.min(), xs.max()
hip_x = (s0 + s1) / 2
P['torso'] = dict(img=t, pivot=(hip_x, hip_y))
legN_x = s0 + (s1 - s0) * .27; legF_x = s0 + (s1 - s0) * .74
sh_y = a0 + 140
rig = dict(
    head=((neck_x - hip_x), (a0 + 30 - hip_y)),
    shoulder=((L + 48 - hip_x), (sh_y - hip_y)), shoulderF=((R - 48 - hip_x), (sh_y - hip_y)),
    leg_near=((legN_x - hip_x), 0), leg_far=((legF_x - hip_x), 0),
    pack=((L + 40 - hip_x), (a0 + 210 - hip_y)))
# ---- 手臂 ----
for nm in ('arm', 'arm_far'):
    a = part(nm); y0, y1 = rows(a)
    py = y0 + (y1 - y0) * .11; hy = y1 - 14
    P[nm] = dict(img=a, pivot=(opaque_center(a, py), py), hand=(opaque_center(a, hy), hy))
# ---- 拳头：原图袖口在左、拳头朝右；转 90°，袖口朝上（手臂垂下时拳头在袖口下面，抬平时拳头朝前）----
for nm in ('hand', 'hand_far'):
    a = np.asarray(Image.fromarray(part(nm)).rotate(-90, expand=True)); y0, y1 = rows(a)
    py = y0 + (y1 - y0) * .14
    P[nm] = dict(img=a, pivot=(opaque_center(a, py), py))
# ---- 腿：裤脚（黑短裤变窄成条纹袜的那一行）对齐身体短裤下沿 ----
for nm in ('leg_near', 'leg_far'):
    a = part(nm); y0, y1 = rows(a)
    wid = [(np.ptp(np.nonzero(a[y, :, 3] > 128)[0]) if (a[y, :, 3] > 128).any() else 0) for y in range(a.shape[0])]
    wmax = max(wid[y0:y0 + 200]); leg_hem = next(y for y in range(y0 + 150, y1) if wid[y] < .62 * wmax)
    py = leg_hem - (hem - hip_y)
    P[nm] = dict(img=a, pivot=(opaque_center(a, max(y0 + 5, py)), py), sole=y1)
legLen = P['leg_near']['sole'] - P['leg_near']['pivot'][1]
# ---- 背包：带子那边（右边）贴后背 ----
pk = part('pack'); y0, y1 = rows(pk); xs = np.nonzero((pk[..., 3] > 128).any(0))[0]
P['pack'] = dict(img=pk, pivot=(xs.max() - 46, y0 + (y1 - y0) * .42))
# ---- 输出 ----
man = dict(scale=SCALE, weapon_class='dagger', parts={}, rig={}, rig_version=2, proportion_profile='kid_v17')
def U(v): return round(float(v) / SRC_PER_UNIT, 2)
for nm, d in P.items():
    img = Image.fromarray(d['img']); W, H = img.size
    img = img.resize((max(1, round(W * K)), max(1, round(H * K))), Image.LANCZOS)
    img.save(os.path.join(OUT, nm + '.png'))
    e = dict(file=nm + '.png', pivot=[U(d['pivot'][0]), U(d['pivot'][1])])
    if 'hand' in d: e['hand'] = [U(d['hand'][0]), U(d['hand'][1])]
    man['parts'][nm] = e
man['shoulder'] = [U(rig['shoulder'][0]), U(rig['shoulder'][1])]; man['shoulderF'] = [U(rig['shoulderF'][0]), U(rig['shoulderF'][1])]
man['rig'] = dict(hip=[-2.5, -U(legLen)], head=[U(rig['head'][0]), U(rig['head'][1])], pack=[U(rig['pack'][0]), U(rig['pack'][1])],
                  leg_near=[U(rig['leg_near'][0]), 0], leg_far=[U(rig['leg_far'][0]), 0])
json.dump(man, open(os.path.join(OUT, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
print(json.dumps(man, ensure_ascii=False))
for nm in P: print(nm, Image.open(os.path.join(OUT, nm + '.png')).size)
