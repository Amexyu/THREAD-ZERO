# 贴图里被包在里面的透明/半透明像素（抠洋红背景时把洋红颜料点也抠掉了一部分）补成不透明，颜色取周围
import sys, numpy as np, cv2
from PIL import Image
def fix(path):
    im = np.asarray(Image.open(path).convert('RGBA')).astype(np.float32); a = im[..., 3]
    out = (a < 128).astype(np.uint8); h, w = out.shape
    pad = np.pad(out, 1, constant_values=1); ff = pad.copy(); mask = np.zeros((h + 4, w + 4), np.uint8)
    cv2.floodFill(ff, mask, (0, 0), 2); exterior = (ff[1:-1, 1:-1] == 2)
    hole = (~exterior) & (a < 250)
    # 只补真正被包住的：离外面至少 2 像素
    dist = cv2.distanceTransform((~exterior).astype(np.uint8), cv2.DIST_L2, 3); hole &= dist > 2.0
    n = int(hole.sum())
    if n:
        opaque = (a >= 250).astype(np.float32); k = np.ones((5, 5), np.float32)
        for c in range(3):
            num = cv2.filter2D(im[..., c] * opaque, -1, k); den = cv2.filter2D(opaque, -1, k)
            fillc = np.where(den > 0, num / np.maximum(den, 1e-3), im[..., c])
            # 半透明的洋红点：原来的颜色还在，按 alpha 混一下就行；几乎全透明的用周围颜色
            wgt = np.clip(a / 255., 0, 1); im[..., c] = np.where(hole, im[..., c] * wgt + fillc * (1 - wgt), im[..., c])
        im[..., 3] = np.where(hole, 255, a)
        Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), 'RGBA').save(path)
    return n
for p in sys.argv[1:]: print(p, '补了', fix(p))
