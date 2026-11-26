"""不靠 Gemini 的接缝（有规律的横条纹理用）：在 [lo·W, W-c] 里找一个宽度 P，使图最左边 c 列和第 P 列开始的 c 列最像，
裁成宽 P 的图，再把第 P..P+c 列交叉淡化进最左边 c 列。警示条纹、系缆桩、栏杆这类等距的东西会自己对齐。
用法: periodcut.py in.png out.png [--c 120] [--lo 0.7] [--rows 0.0-1.0]"""
import sys, numpy as np
from PIL import Image
def run(src, dst, c=120, lo=.7, rows=(0., 1.)):
    im = Image.open(src); mode = im.mode
    a = np.asarray(im.convert('RGBA')).astype(np.float32); H, W = a.shape[:2]
    y0, y1 = int(rows[0] * H), int(rows[1] * H)
    ref = a[y0:y1, :c, :3]
    best = None
    for P in range(int(lo * W), W - c):
        d = np.abs(a[y0:y1, P:P + c, :3] - ref).mean()
        if best is None or d < best[0]: best = (d, P)
    d, P = best
    out = a[:, :P].copy()
    t = (np.arange(c) + .5) / c; t = t * t * (3 - 2 * t)
    out[:, :c] = a[:, :c] * t[None, :, None] + a[:, P:P + c] * (1 - t[None, :, None])
    o = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(o if mode == 'RGBA' else o[..., :3], 'RGBA' if mode == 'RGBA' else 'RGB').save(dst)
    print(dst, '宽', P, '差', round(float(d), 2))
if __name__ == '__main__':
    a = sys.argv[1:]
    c = int(a[a.index('--c') + 1]) if '--c' in a else 120
    lo = float(a[a.index('--lo') + 1]) if '--lo' in a else .7
    rows = tuple(map(float, a[a.index('--rows') + 1].split('-'))) if '--rows' in a else (0., 1.)
    run(a[0], a[1], c, lo, rows)
