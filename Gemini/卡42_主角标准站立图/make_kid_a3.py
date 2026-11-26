# 卡42 v3：标准站立图 A → 带手肘、膝盖、脚踝的部件（同一张图上拆，关节点都在原图坐标里量）。
# 输出 部件A3/：torso head upper fore hand thigh shin foot（远侧 *_far 用同一张，游戏里压暗）+ parts.json（rig_version 3）。
import os, sys, json, numpy as np, cv2
from PIL import Image
OUT = sys.argv[1] if len(sys.argv) > 1 else '部件A3'; os.makedirs(OUT, exist_ok=True)
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
def disc(c, r): return (xx - c[0]) ** 2 + (yy - c[1]) ** 2 <= r * r
# ---- 关节（原图像素）----
NECK = (450, 405); HIP = (370, 960)
SH = (290, 510); ELB = (285, 695); WR = (310, 880)
HIPN = (330, 1000); KNEEN = (330, 1190); ANKN = (332, 1378); BALLN = (468, 1428)
HIPF = (410, 1000); KNEEF = (410, 1185); ANKF = (430, 1365); BALLF = (540, 1412)
GROUND = 1445; ROOTX = 395
# ---- 头 ----
cut = np.interp(xx, [0, 200, 300, 380, 430, 470, 520, 560, W], [330, 330, 345, 372, 395, 408, 415, 420, 420])
headm = (yy < cut) & (xx > 150)
# ---- 身体：A，手臂那块换成 B；不含头（留重叠）、短裤下沿以下 ----
HEM = 1098
T = A.copy(); fillm = cv2.dilate(arm.astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool); T[fillm] = B[fillm]
T[(yy < cut - 14) & (xx > 150), 3] = 0; T[yy > HEM + 12, 3] = 0
# ---- 手臂：上臂 / 前臂各带一个圆头，手肘处叠着 ----
R_E = 92
upper = sleeve & ((yy < ELB[1]) | disc(ELB, R_E)); fore = sleeve & ((yy >= ELB[1]) | disc(ELB, R_E))
# ---- 腿：两条腿中间的分界线、近侧鞋的多边形（同 make_kid_a.py）----
xs = np.interp(yy, [HEM, 1250], [384, 366])
shoeP = np.array([(262, 1280), (300, 1279), (338, 1266), (392, 1266), (398, 1300), (470, 1318), (502, 1338), (528, 1366), (534, 1412), (522, 1446), (262, 1446)], np.int32)
P = np.zeros((H, W), np.uint8); cv2.fillPoly(P, [shoeP], 1); P = P.astype(bool)
fig = A[..., 3] > 128
legN = fig & (yy > HEM) & (((yy <= 1262) & (xx < xs)) | ((yy > 1262) & P))
legF = fig & (yy > HEM) & ~legN
footN = legN & (yy > 1258) & P; footF = legF & (yy > 1240) & (xx > 395)
shinN = legN & ~footN; shinF = legF & ~footF
def layer(src, m): L = np.zeros_like(src); L[m] = src[m]; return L
LN_shin = layer(A, shinN); LF_shin = layer(A, shinF); LN_foot = layer(A, footN); LF_foot = layer(A, footF)
# 11-26 改：大腿 / 小腿改用程序画的紧身裤胶囊（原图里大腿藏在短裤下面、两条腿互相挡着，拆出来的图会断），
# 两条腿用同一套图（远侧在游戏里压暗）；鞋用原图里完整的那只近侧鞋。
from leg_capsule import capsule
def cap_layer(img, top, bot, at_top, at_bot):
    """把胶囊图（自己的像素坐标）放进原图大小的画布里，让 top 对到 at_top（只平移，长度已经按原图量好）。"""
    L = np.zeros_like(A); a = np.asarray(img).astype(np.float32); x0 = int(round(at_top[0] - top[0])); y0 = int(round(at_top[1] - top[1]))
    h, w = a.shape[:2]; L[y0:y0 + h, x0:x0 + w] = a; return L
lenT = int(round(np.hypot(KNEEN[0] - HIPN[0], KNEEN[1] - HIPN[1]))); lenS = int(round(np.hypot(ANKN[0] - KNEEN[0], ANKN[1] - KNEEN[1])))
ci, ct, cb = capsule(lenT, 92, 84); TN = cap_layer(ci, ct, cb, HIPN, KNEEN)
si, st_, sb = capsule(lenS, 84, 72); LN_shin = cap_layer(si, st_, sb, KNEEN, ANKN)
TF = cap_layer(ci, ct, cb, HIPF, (HIPF[0], HIPF[1] + lenT)); LF_shin = cap_layer(si, st_, sb, (HIPF[0], HIPF[1] + lenT), (HIPF[0], HIPF[1] + lenT + lenS))
KNEEF = (HIPF[0], HIPF[1] + lenT); ANKF = (HIPF[0], HIPF[1] + lenT + lenS)
LF_foot = np.zeros_like(A); dx, dy = ANKF[0] - ANKN[0], ANKF[1] - ANKN[1]
src = LN_foot; ys, xs_ = np.nonzero(src[..., 3] > 0); LF_foot[ys + dy, xs_ + dx] = src[ys, xs_]
BALLF = (BALLN[0] + dx, BALLN[1] + dy)
# ---- 输出 ----
FPU = 10.25; K = 5.0 / FPU
def save(L, name):
    a = L[..., 3] > 8; ys, xs2 = np.nonzero(a); x0, x1, y0, y1 = xs2.min() - 4, xs2.max() + 5, ys.min() - 4, ys.max() + 5
    im = Image.fromarray(np.clip(L[y0:y1, x0:x1], 0, 255).astype(np.uint8)); im = im.resize((max(1, round(im.width * K)), max(1, round(im.height * K))), Image.LANCZOS)
    im.save(os.path.join(OUT, name + '.png')); return (x0, y0)
def U(v): return round(v / FPU, 2)
o = {}
for name, L in (('torso', T), ('head', layer(A, headm)), ('upper', layer(A, upper)), ('fore', layer(A, fore)), ('hand', layer(A, hand)),
                ('thigh', TN), ('shin', LN_shin), ('foot', LN_foot), ('thigh_far', TF), ('shin_far', LF_shin), ('foot_far', LF_foot)): o[name] = save(L, name)
for n in ('upper', 'fore', 'hand'): Image.open(os.path.join(OUT, n + '.png')).save(os.path.join(OUT, n + '_far.png')); o[n + '_far'] = o[n]
Image.new('RGBA', (2, 2), (0, 0, 0, 0)).save(os.path.join(OUT, 'pack.png'))
def P2(name, p): return [U(p[0] - o[name][0]), U(p[1] - o[name][1])]
def E(name, piv, child=None):
    e = dict(file=name + '.png', pivot=P2(name, piv))
    if child: e['hand'] = P2(name, child)
    return e
parts = dict(torso=E('torso', HIP), head=dict(E('head', NECK), eye=[U(545 - NECK[0]), U(265 - NECK[1])]),
             upper=E('upper', SH, ELB), fore=E('fore', ELB, WR), hand=E('hand', WR), upper_far=E('upper_far', SH, ELB), fore_far=E('fore_far', ELB, WR), hand_far=E('hand_far', WR),
             thigh=E('thigh', HIPN, KNEEN), shin=E('shin', KNEEN, ANKN), foot=dict(E('foot', ANKN), tip=P2('foot', BALLN)),
             thigh_far=E('thigh_far', HIPF, KNEEF), shin_far=E('shin_far', KNEEF, ANKF), foot_far=dict(E('foot_far', ANKF), tip=P2('foot_far', BALLF)),
             pack=dict(file='pack.png', pivot=[0, 0]))
# 旧骨骼需要的部件名（arm / leg_near ...）也写上，旧代码读得到；新代码看到 upper / thigh 就走新骨骼
parts.update(arm=parts['upper'], arm_far=parts['upper_far'], leg_near=parts['thigh'], leg_far=parts['thigh_far'])
man = dict(scale=5.0, weapon_class='dagger', rig_version=3, far_shade=.72, bar_lift=8, proportion_profile='kid_standard_a3', parts=parts,
           shoulder=[U(SH[0] - HIP[0]), U(SH[1] - HIP[1])], shoulderF=[U(SH[0] + 12 - HIP[0]), U(SH[1] + 2 - HIP[1])],
           rig=dict(hip=[U(HIP[0] - ROOTX), U(HIP[1] - GROUND)], head=[U(NECK[0] - HIP[0]), U(NECK[1] - HIP[1])], pack=[0, 0],
                    leg_near=[U(HIPN[0] - HIP[0]), U(HIPN[1] - HIP[1])], leg_far=[U(HIPF[0] - HIP[0]), U(HIPF[1] - HIP[1])]))
json.dump(man, open(os.path.join(OUT, 'parts.json'), 'w'), ensure_ascii=False, indent=1)
print(json.dumps(man, ensure_ascii=False)[:1500])
