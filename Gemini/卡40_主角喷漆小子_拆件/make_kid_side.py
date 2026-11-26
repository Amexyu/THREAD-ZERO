# 卡40：侧身版（11-26 用户：做好了再定）。拆件来自「拆件_侧身_原图」（Gemini 照设定图画的，原图朝左，这里左右翻成朝右）；
# 头沿用 v2 的侧脸头（部件/head.png）。原图像素 × K = 游戏像素（1 单位 = 5 像素）。点位是翻转后的原图像素。
import os, sys, json, numpy as np
from PIL import Image, ImageOps
CUT = '切图侧身'; OUT = sys.argv[1] if len(sys.argv) > 1 else '部件侧身'; os.makedirs(OUT, exist_ok=True)
allim = np.asarray(Image.open(os.path.join(CUT, '_all.png'))); lab = np.load(os.path.join(CUT, '_lab.npy'))
comps = {c['i']: c for c in json.load(open(os.path.join(CUT, '_comps.json')))}
SC = 5.0; K = float(os.environ.get('K', .332)); KF = float(os.environ.get('KF', .27)); KH = float(os.environ.get('KH', .864))
NECK = (290, 70); SHOULDER = (230, 210); HIP = (215, 700); BACK = (30, 280)   # 身体上的点（翻转后的原图像素）
def part(i, pad=6):
    c = comps[i]; x0, y0, x1, y1 = max(0, c['x'] - pad), max(0, c['y'] - pad), c['x'] + c['w'] + pad, c['y'] + c['h'] + pad
    a = allim[y0:y1, x0:x1].copy(); m = lab[y0:y1, x0:x1] == i; a[..., 3] = np.where(m, a[..., 3], 0)
    return ImageOps.mirror(Image.fromarray(a))
def rows(im): a = np.asarray(im)[..., 3] > 128; ys = np.nonzero(a.any(1))[0]; return ys.min(), ys.max()
def cx(im, y): a = np.asarray(im)[int(y), :, 3] > 128; xs = np.nonzero(a)[0]; return (xs.min() + xs.max()) / 2 if len(xs) else im.width / 2
def save(im, name, k): out = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS); out.save(os.path.join(OUT, name + '.png')); return out
U = lambda v: round(v / SC, 2)
man = dict(scale=SC, weapon_class='dagger', parts={}, rig={}, rig_version=2, far_shade=.9, proportion_profile='kid_side')
# 身体
t = part(1); t0, t1 = rows(t); save(t, 'torso', K)
hip = (HIP[0] * K, HIP[1] * K); hem = t1 * K
man['parts']['torso'] = dict(file='torso.png', pivot=[U(hip[0]), U(hip[1])])
def rel(p): return [U(p[0] * K - hip[0]), U(p[1] * K - hip[1])]
man['rig']['head'] = rel(NECK); man['rig']['pack'] = rel(BACK)
man['shoulder'] = rel(SHOULDER); man['shoulderF'] = rel((SHOULDER[0] + 6, SHOULDER[1] - 4))
man['rig']['leg_near'] = [.6, 0]; man['rig']['leg_far'] = [-.6, 0]
# 腿：裤脚（黑短裤变成条纹袜那一行）对齐身体短裤下沿
legLen = 0
for nm, ci in (('leg_near', 9), ('leg_far', 10)):
    l = part(ci); y0, y1 = rows(l); a = np.asarray(l)[..., 3] > 128
    wid = [np.ptp(np.nonzero(a[y])[0]) if a[y].any() else 0 for y in range(l.height)]
    wmax = max(wid[y0:y0 + 300]); lhem = next(y for y in range(y0 + 200, y1) if wid[y] < .6 * wmax)
    py = lhem * K - (hem - hip[1])   # 游戏像素
    save(l, nm, K)
    man['parts'][nm] = dict(file=nm + '.png', pivot=[U(cx(l, max(y0 + 4, py / K)) * K), U(py)])
    legLen = max(legLen, y1 * K - py)
man['rig']['hip'] = [-1.5, -U(legLen)]
# 袖子
for nm, ci in (('arm', 3), ('arm_far', 4)):
    s = part(ci); y0, y1 = rows(s); py = y0 + (y1 - y0) * .09; hy = y1 - 10
    save(s, nm, K); man['parts'][nm] = dict(file=nm + '.png', pivot=[U(cx(s, py) * K), U(py * K)], hand=[U(cx(s, hy) * K), U(hy * K)])
# 拳头：原图袖口在下、拳头朝上 → 转 180°，袖口朝上
for nm, ci in (('hand', 11), ('hand_far', 12)):
    f = part(ci).rotate(180, expand=True); y0, y1 = rows(f); py = y0 + (y1 - y0) * .12
    save(f, nm, KF); man['parts'][nm] = dict(file=nm + '.png', pivot=[U(cx(f, py) * KF), U(py * KF)])
# 背包：右边（贴背的一边）中间偏上
p = part(2); y0, y1 = rows(p); a = np.asarray(p)[..., 3] > 128; xs = np.nonzero(a.any(0))[0]
save(p, 'pack', K); man['parts']['pack'] = dict(file='pack.png', pivot=[U((xs.max() - 30) * K), U((y0 + (y1 - y0) * .38) * K)])
# 头：沿用 v2 的侧脸，缩到和身体配
h = Image.open('部件/head.png'); hk = json.load(open('部件/parts.json'))['parts']['head']
h.resize((round(h.width * KH), round(h.height * KH)), Image.LANCZOS).save(os.path.join(OUT, 'head.png'))
man['parts']['head'] = dict(file='head.png', pivot=[round(hk['pivot'][0] * KH, 2), round(hk['pivot'][1] * KH, 2)], eye=[round(8.2 * KH, 2), round(-15.0 * KH, 2)])
json.dump(man, open(os.path.join(OUT, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
print(json.dumps(man, ensure_ascii=False))
