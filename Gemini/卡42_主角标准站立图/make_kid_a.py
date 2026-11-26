# 卡42：标准站立图 A（用户 11-26 00:38 选定）→ 游戏骨骼部件。所有部件都从同一张图上拆，关节位置天然对得上。
#   A = 出图/_A.png（原图裁出来的人），B = 出图/_B_aligned.png（Gemini 去掉近侧手臂后对齐回 A），手臂 / 手的遮罩 = 出图/_armmask.npy / _handmask.npy。
import os, sys, json, numpy as np, cv2
from PIL import Image
OUT = sys.argv[1] if len(sys.argv) > 1 else '部件A'; os.makedirs(OUT, exist_ok=True)
def rgba(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = np.clip((np.minimum(r, b) - g - .6 * np.abs(r - b) - 40) / 60, 0, 1); al = 1 - bg
    col = np.clip((a - bg[..., None] * np.array([255, 0, 255], np.float32)) / np.maximum(al, 1e-3)[..., None], 0, 255)
    e = (al < .98) & (al > 0); gg = col[..., 1]
    col[..., 0] = np.where(e, np.minimum(col[..., 0], gg + 60), col[..., 0]); col[..., 2] = np.where(e, np.minimum(col[..., 2], gg + 60), col[..., 2])
    return np.dstack([col, al * 255]).astype(np.float32)
A = rgba('出图/_A.png'); B = rgba('出图/_B_aligned.png'); H, W = A.shape[:2]
arm = np.load('出图/_armmask.npy').astype(bool); hand = np.load('出图/_handmask.npy').astype(bool); sleeve = arm & ~hand
yy, xx = np.mgrid[0:H, 0:W]
# ---- 头：沿方巾上沿切开 ----
cut = np.interp(xx, [0, 200, 300, 380, 430, 470, 520, 560, W], [330, 330, 345, 372, 395, 408, 415, 420, 420])
headm = (yy < cut) & (xx > 150)
# ---- 腿：短裤下沿以下；两条腿中间那条竖线分开，近侧鞋用多边形 ----
HEM = 1098
xs = np.interp(yy, [HEM, 1250], [384, 366])
shoe = np.array([(262, 1280), (300, 1279), (338, 1266), (392, 1266), (398, 1300), (470, 1318), (502, 1338), (528, 1366), (534, 1412), (522, 1446), (262, 1446)], np.int32)
P = np.zeros((H, W), np.uint8); cv2.fillPoly(P, [shoe], 1); P = P.astype(bool)
legs = yy > HEM
nearm = legs & (((yy <= 1262) & (xx < xs)) | ((yy > 1262) & P))
farm = legs & ~nearm
# ---- 身体：A，手臂那块换成 B（去掉手臂后补画的卫衣），去掉头（留一条 14 像素的重叠）和腿 ----
T = A.copy(); fillm = cv2.dilate(arm.astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool)
T[fillm] = B[fillm]
T[(yy < cut - 14) & (xx > 150), 3] = 0
T[yy > HEM + 12, 3] = 0
def layer(src, m): L = np.zeros_like(src); L[m] = src[m]; return L
head = layer(A, headm); sl = layer(A, sleeve); hd = layer(A, hand)
ln = layer(A, nearm); lf = layer(A, farm)
# 远侧腿被近侧鞋挡住的那块：用周围的像素补（走路时两只脚分开才会露出来）
hid = (P & legs & (xx > 380)).astype(np.uint8)
rgb = lf[..., :3].astype(np.uint8).copy(); a8 = lf[..., 3].astype(np.uint8)
known = (a8 > 128).astype(np.uint8); hole = (hid & (1 - known)).astype(np.uint8)
farshoe_zone = np.zeros((H, W), np.uint8); cv2.fillPoly(farshoe_zone, [np.array([(400, 1255), (600, 1330), (600, 1428), (400, 1440)], np.int32)], 1)
hole &= farshoe_zone
# 补成白色鞋面，再沿外轮廓描一圈黑线（和原画的粗黑线一样宽）
full = ((known > 0) | (hole > 0)).astype(np.uint8)
edge = (full - cv2.erode(full, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11)))).astype(bool) & (hole > 0)
lf[hole > 0] = (248, 247, 243, 255); lf[edge] = (14, 12, 16, 255)
# 腿往上延到短裤里面（被短裤盖住，转动时不露缝）
for L, mk in ((ln, nearm), (lf, farm)):
    row = HEM + 4; cols = np.nonzero(L[row, :, 3] > 128)[0]
    if len(cols): L[960:row, cols.min():cols.max() + 1] = L[row, cols.min():cols.max() + 1]
# ---- 骨骼点（原图像素）----
HIP = (395, 960); NECK = (450, 405); SH = (300, 495); WRIST = (330, 885); LEGN = (345, 1010); LEGF = (410, 1010); ROOTX, GROUND = 425, 1445
FPU = 10.25; K = 5.0 / FPU   # 原图 10.25 像素 = 1 单位 = 游戏 5 像素
def save(L, name):
    a = L[..., 3] > 8; ys, xs2 = np.nonzero(a); x0, x1, y0, y1 = xs2.min() - 4, xs2.max() + 5, ys.min() - 4, ys.max() + 5
    im = Image.fromarray(L[y0:y1, x0:x1].astype(np.uint8)); im = im.resize((max(1, round(im.width * K)), max(1, round(im.height * K))), Image.LANCZOS)
    im.save(os.path.join(OUT, name + '.png')); return (x0, y0)
def U(v): return round(v / FPU, 2)
o = {}
for name, L in (('torso', T), ('head', head), ('arm', sl), ('hand', hd), ('leg_near', ln), ('leg_far', lf)): o[name] = save(L, name)
im = Image.open(os.path.join(OUT, 'arm.png')); im.save(os.path.join(OUT, 'arm_far.png')); Image.open(os.path.join(OUT, 'hand.png')).save(os.path.join(OUT, 'hand_far.png'))
Image.new('RGBA', (2, 2), (0, 0, 0, 0)).save(os.path.join(OUT, 'pack.png'))
def piv(name, p): return [U(p[0] - o[name][0]), U(p[1] - o[name][1])]
man = dict(scale=5.0, weapon_class='dagger', rig_version=2, far_shade=.72, proportion_profile='kid_standard_a',
  parts=dict(torso=dict(file='torso.png', pivot=piv('torso', HIP)), head=dict(file='head.png', pivot=piv('head', NECK), eye=[U(545 - NECK[0]), U(265 - NECK[1])]),
             arm=dict(file='arm.png', pivot=piv('arm', SH), hand=piv('arm', WRIST)), arm_far=dict(file='arm_far.png', pivot=piv('arm', SH), hand=piv('arm', WRIST)),
             hand=dict(file='hand.png', pivot=piv('hand', WRIST)), hand_far=dict(file='hand_far.png', pivot=piv('hand', WRIST)),
             leg_near=dict(file='leg_near.png', pivot=piv('leg_near', LEGN)), leg_far=dict(file='leg_far.png', pivot=piv('leg_far', LEGF)),
             pack=dict(file='pack.png', pivot=[0, 0])),
  shoulder=[U(SH[0] - HIP[0]), U(SH[1] - HIP[1])], shoulderF=[U(SH[0] + 10 - HIP[0]), U(SH[1] + 2 - HIP[1])],
  rig=dict(hip=[U(HIP[0] - ROOTX), U(HIP[1] - GROUND)], head=[U(NECK[0] - HIP[0]), U(NECK[1] - HIP[1])], pack=[0, 0],
           leg_near=[U(LEGN[0] - HIP[0]), U(LEGN[1] - HIP[1])], leg_far=[U(LEGF[0] - HIP[0]), U(LEGF[1] - HIP[1])]))
json.dump(man, open(os.path.join(OUT, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
print(json.dumps(man, ensure_ascii=False))
