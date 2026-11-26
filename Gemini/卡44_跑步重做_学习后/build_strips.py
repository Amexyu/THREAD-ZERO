# 卡44：从标准站立图 A 取「一整条」腿和袖子做网格变形用的贴图（不再是两节胶囊/两节袖子拼起来）。
# 腿：A 里近侧腿露出来的那段（短裤下沿到鞋口）是手画的；藏在短裤里和鞋里的部分按原图往上/往下镜像延长。
# 袖子：A 里近侧整只袖子（肩到袖口），手另外一块。
import os, json, numpy as np, cv2
from PIL import Image
SRC = '../卡42_主角标准站立图/出图'; OUT = '条带'; os.makedirs(OUT, exist_ok=True)
FPU = 10.25; SC = 5.0; K = SC / FPU          # 原图 → 贴图（5 像素/单位，和部件一样）
def rgba(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = np.clip((np.minimum(r, b) - g - .6 * np.abs(r - b) - 40) / 60, 0, 1); al = 1 - bg
    col = np.clip((a - bg[..., None] * np.array([255, 0, 255], np.float32)) / np.maximum(al, 1e-3)[..., None], 0, 255)
    e = (al < .98) & (al > 0); gg = col[..., 1]
    col[..., 0] = np.where(e, np.minimum(col[..., 0], gg + 60), col[..., 0]); col[..., 2] = np.where(e, np.minimum(col[..., 2], gg + 60), col[..., 2])
    return np.dstack([col, al * 255]).astype(np.float32)
A = rgba(os.path.join(SRC, '_A.png')); H, W = A.shape[:2]; yy, xx = np.mgrid[0:H, 0:W]
HIPN = (330, 1000); KNEEN = (330, 1190); ANKN = (332, 1378)
SH = (290, 510); ELB = (285, 695); WR = (310, 880)

# ---------- 腿 ----------
# A 里近侧腿露在外面、干净的一段：1132~1268 行（再往上是短裤的阴影，再往下是鞋口）。
# 做法：给整条腿一个自然的轮廓（大腿粗、膝盖略收、小腿肚往后鼓、脚踝细），每一行从 A 的同一行（或干净段里循环）按左右边缘拉过来。
HEM = 1098; C0, C1 = 1132, 1268; PER = 56
xs = np.interp(yy, [HEM, 1250], [384, 366]); fig = A[..., 3] > 40
edges = {}
for y in range(C0, C1 + 1):
    row = fig[y] & (xx[y] < xs[y] + 6) & (xx[y] > 240); cols = np.nonzero(row)[0]; edges[y] = (cols.min(), cols.max())
def pchip(xk, yk, x):
    from numpy import interp
    xk = np.asarray(xk, float); yk = np.asarray(yk, float); h = np.diff(xk); d = np.diff(yk) / h; m = np.zeros(len(xk))
    for i in range(1, len(xk) - 1):
        m[i] = 0 if d[i - 1] * d[i] <= 0 else (3 * (h[i - 1] + h[i])) / ((2 * h[i] + h[i - 1]) / d[i - 1] + (h[i] + 2 * h[i - 1]) / d[i])
    m[0] = d[0]; m[-1] = d[-1]; i = np.clip(np.searchsorted(xk, x) - 1, 0, len(xk) - 2); t = (x - xk[i]) / h[i]
    return (2 * t ** 3 - 3 * t ** 2 + 1) * yk[i] + (t ** 3 - 2 * t ** 2 + t) * h[i] * m[i] + (-2 * t ** 3 + 3 * t ** 2) * yk[i + 1] + (t ** 3 - t ** 2) * h[i] * m[i + 1]
PY = [930, 1000, 1110, 1180, 1190, 1235, 1290, 1378, 1440]
PWL = [53, 53, 50, 47, 47, 50, 45, 37, 35]          # 中线往后（左）多宽：小腿肚往后鼓
PWR = [53, 53, 49, 48, 47, 45, 42, 36, 34]          # 往前（右）多宽：膝盖略鼓
CX = 330.0
yA, yB = 930, 1440; x0, x1 = 262, 404
# 照 A 的紧身裤画：深灰底 + 黑网点（7 像素一格）+ 偏后的一道白色网点条纹 + 两侧粗细有变化的黑描边（不画上下两头，藏在短裤和鞋里）
SS = 4; Hh, Ww = (yB - yA) * SS, (x1 - x0) * SS
Y, X = np.mgrid[0:Hh, 0:Ww].astype(np.float32); Ya = yA + (Y + .5) / SS; Xa = x0 + (X + .5) / SS
ys1 = np.arange(yA, yB, .25)
OL = np.interp(Ya, ys1, [CX - pchip(PY, PWL, y) for y in ys1]); OR = np.interp(Ya, ys1, [CX + pchip(PY, PWR, y) for y in ys1])
rng = np.random.default_rng(7)
def smooth_noise(n, k): v = np.convolve(rng.normal(0, 1, n + 2 * k), np.ones(k) / k, 'same')[k:-k]; return v / (np.abs(v).max() + 1e-6)
nL = np.interp(Ya, np.arange(yA, yB), smooth_noise(yB - yA, 45)); nR = np.interp(Ya, np.arange(yA, yB), smooth_noise(yB - yA, 45))
OL = OL + .8 * nL; OR = OR + .8 * nR                                   # 轮廓轻微不规则（手画感）
thL = 7.0 + 1.6 * np.interp(Ya, np.arange(yA, yB), smooth_noise(yB - yA, 60)); thR = 6.5 + 1.6 * np.interp(Ya, np.arange(yA, yB), smooth_noise(yB - yA, 60))
t = (Xa - OL) / (OR - OL); inside = (Xa >= OL) & (Xa <= OR)
col = np.zeros((Hh, Ww, 3), np.float32); col[:] = (58, 57, 60)
stripe = (t > .33) & (t < .47); col[stripe] = (236, 236, 232)
p = 7.0; gx = (Xa % p) - p / 2; gy = ((Ya + (np.floor(Xa / p) % 2) * p / 2) % p) - p / 2; dot = np.sqrt(gx * gx + gy * gy) < (1.5 + .5 * (t < .25))
col[dot & ~stripe] = (24, 23, 26); col[dot & stripe] = (128, 128, 126)
# 前侧（右）一点点亮边，后侧（左）网点更密 = 体积
col[(t > .82) & ~dot] = (74, 73, 76)
ink = (Xa - OL < thL) | (OR - Xa < thR)
col[ink] = (14, 12, 16)
al = inside.astype(np.float32) * 255
big = np.dstack([col, al])
strip = cv2.resize(big, (x1 - x0, yB - yA), interpolation=cv2.INTER_AREA)
leg = cv2.resize(strip, (int(round(strip.shape[1] * K)), int(round(strip.shape[0] * K))), interpolation=cv2.INTER_AREA)
J = lambda p: [round(float((p[0] - x0) * K / SC), 3), round(float((p[1] - yA) * K / SC), 3)]   # → 单位
Image.fromarray(np.clip(leg, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'leg.png'))
meta = dict(scale=SC, leg=dict(file='leg.png', joints=[J(HIPN), J(KNEEN), J(ANKN)]))

# ---------- 袖子 ----------
arm = np.load(os.path.join(SRC, '_armmask.npy')).astype(bool); hand = np.load(os.path.join(SRC, '_handmask.npy')).astype(bool)
sleeve = arm & ~hand
ys_, xs_ = np.nonzero(sleeve); ax0, ax1, ay0, ay1 = xs_.min() - 6, xs_.max() + 7, ys_.min() - 6, ys_.max() + 7
sl = np.zeros_like(A); sl[sleeve] = A[sleeve]
# 手那块挖掉以后，袖口下沿是开着的：保留（手画在袖口下面会盖住）
sl = sl[ay0:ay1, ax0:ax1]
sl_s = cv2.resize(sl, (int(round(sl.shape[1] * K)), int(round(sl.shape[0] * K))), interpolation=cv2.INTER_AREA)
Image.fromarray(np.clip(sl_s, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, 'sleeve.png'))
Js = lambda p: [round(float((p[0] - ax0) * K / SC), 3), round(float((p[1] - ay0) * K / SC), 3)]
meta['sleeve'] = dict(file='sleeve.png', joints=[Js(SH), Js(ELB), Js(WR)])
json.dump(meta, open(os.path.join(OUT, 'strips.json'), 'w'), indent=1)
print(meta, leg.shape, sl_s.shape)
# 预览：放大 3 倍，标关节
def prev(img, js, path):
    im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), 'RGBA'); im = im.resize((im.width * 3, im.height * 3), Image.NEAREST)
    bg = Image.new('RGBA', im.size, (200, 230, 200, 255)); bg.alpha_composite(im)
    from PIL import ImageDraw; d = ImageDraw.Draw(bg)
    for j in js: x, y = j[0] * SC * 3, j[1] * SC * 3; d.ellipse([x - 5, y - 5, x + 5, y + 5], outline=(255, 0, 0, 255), width=2)
    return bg
a = prev(leg, meta['leg']['joints'], None); b = prev(sl_s, meta['sleeve']['joints'], None)
s = Image.new('RGBA', (a.width + b.width + 20, max(a.height, b.height)), (255, 255, 255, 255)); s.alpha_composite(a, (0, 0)); s.alpha_composite(b, (a.width + 20, 0)); s.save('out/_strips_preview.png')
