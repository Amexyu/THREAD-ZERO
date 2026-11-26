"""（v0.38）Gemini 原图 → 补缝前的底图（宽 1835）。
  far   <raw> <out.png>                      不透明，直接缩到 1835×1024
  mid   <raw> <out.png> [--glow [--glowt 45]]  key2 抠品红 + 紫边去色，按预乘缩到 1835×1024
        --glow：灯泡 / 灯笼的白光晕画在品红上（粉色一团）时，反混合成半透明暖白光；墙是淡紫色、灯笼是粉色时加 --glowt 45
  ground <raw> <out.png> [--skip 3]          从品红结束后 skip 行裁到底，缩到宽 1835"""
import sys, numpy as np
from PIL import Image
import os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'bg')); from key2 import key2
import cv2
def despill_edges(rgb, a):
    """透明缝边上的品红晕（2–4 像素的紫边）：离透明处 6 像素内、偏品红的像素把红蓝拉回到绿的水平（变成中性的描边色），
    特别粉、又紧贴透明处的直接透明。"""
    tr = (a < .5).astype(np.uint8)
    if tr.sum() == 0: return rgb, a
    dist = cv2.distanceTransform(1 - tr, cv2.DIST_L2, 3)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    ex = np.clip(np.minimum(r, b) - g, 0, None)
    fr = (dist <= 6) & (a > 0) & (ex > 8)
    rgb = rgb.copy(); rgb[..., 0] = np.where(fr, r - ex, r); rgb[..., 2] = np.where(fr, b - ex, b)
    strong = fr & (ex > 70) & (dist <= 2.5)
    a = np.where(strong, 0, a)
    print('紫边去色：', int(fr.sum()), '像素，其中透明', int(strong.sum()))
    return rgb, a

W = 1835
GLOW = '--glow' in sys.argv
GLOWT = float(sys.argv[sys.argv.index('--glowt') + 1]) if '--glowt' in sys.argv else 25.0   # 光晕判定：红、蓝都比绿高这么多（墙是淡紫色时调高到 40）
def far(src, dst):
    Image.open(src).convert('RGB').resize((W, 1024), Image.LANCZOS).save(dst, optimize=True)
def mid(src, dst):
    a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
    k = key2(a); rgb = k[..., :3].astype(np.float32); al = k[..., 3].astype(np.float32) / 255
    rgb, al = despill_edges(rgb, al)
    if GLOW:
        # v0.37 灯泡光晕：Gemini 把白色光晕画在品红底上（混成粉色一团）。按「绿通道 = 白光的比例」反混合成半透明暖白光。
        a0 = a
        r, g, b = a0[..., 0], a0[..., 1], a0[..., 2]
        if GLOWT > 30:
            # 墙是淡紫色、灯笼是粉色时：只取“和透明处连着”的偏品红色块（灯笼本身被黑描边围住，连不到透明处）
            cand = (np.minimum(r, b) - g > GLOWT) & (b > 150) & (r > 200) & (al > .5)
            # 先开运算把描边缺口处的细连接断开（不然光晕会顺着缺口连到粉灯笼身上），再按“连着透明处”挑光晕块
            op = cv2.morphologyEx(cand.astype(np.uint8), cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
            n, lab = cv2.connectedComponents(op, connectivity=8)
            edge = cv2.dilate((al < .1).astype(np.uint8), np.ones((7, 7), np.uint8)).astype(bool) & (op > 0)
            ids = np.unique(lab[edge]); ids = ids[ids > 0]
            halo = np.isin(lab, ids)
            for _ in range(3): halo = cv2.dilate(halo.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & cand
        else:
            halo = (r > 200) & (b > 200) & (g < 235) & (r - g > GLOWT) & (b - g > GLOWT) & (al > .5)   # 只管抠图留下来的粉色团
        ga = np.clip((g - 12) / (255 - 12), 0, 1)
        rgb = np.where(halo[..., None], np.array([255, 244, 205], np.float32), rgb)
        al = np.where(halo, ga * .85, al)
        near = cv2.dilate(halo.astype(np.uint8), np.ones((9, 9), np.uint8)).astype(bool) & ~halo & (al > .3)
        ex = np.clip(np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1], 0, None)
        fix = near & (ex > 18)
        rgb = rgb.copy(); rgb[..., 0] = np.where(fix, rgb[..., 0] - ex * .8, rgb[..., 0]); rgb[..., 2] = np.where(fix, rgb[..., 2] - ex * .8, rgb[..., 2])
        print('光晕像素', int(halo.sum()), '光晕旁去紫', int(fix.sum()))
    o = np.dstack([np.clip(rgb, 0, 255), np.clip(al * 255, 0, 255)]).astype(np.uint8)
    im = Image.fromarray(o, 'RGBA').convert('RGBa').resize((W, 1024), Image.LANCZOS).convert('RGBA')
    b = np.asarray(im).copy(); b[b[..., 3] == 0, :3] = 0
    Image.fromarray(b, 'RGBA').save(dst, optimize=True)
def ground(src, dst, skip=3):
    a = np.asarray(Image.open(src).convert('RGB')).astype(np.int32); H = a.shape[0]
    mg = (a[..., 0] > 200) & (a[..., 2] > 200) & (a[..., 1] < 80)
    frac = mg.mean(1)
    y = int(np.argmax(frac < .5)) + skip
    print('品红到第', y - skip, '行；从', y, '行裁起')
    im = Image.fromarray(a[y:].astype(np.uint8)); h = round((H - y) * W / a.shape[1])
    im.resize((W, h), Image.LANCZOS).save(dst, optimize=True); print(dst, (W, h))
if __name__ == '__main__':
    c, s, d = sys.argv[1:4]
    if c == 'far': far(s, d)
    elif c == 'mid': mid(s, d)
    else: ground(s, d, int(sys.argv[sys.argv.index('--skip') + 1]) if '--skip' in sys.argv else 3)
