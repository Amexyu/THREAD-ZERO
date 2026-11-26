"""不靠 Gemini 的接缝：把图最右边 c 像素和最左边 c 像素交叉淡化，输出宽 W-c 的无缝平铺图。
适合天空、海面、路面这类没有大物件的纹理（有物件会出现重影）。
用法: xfade.py in.png out.png [--c 300]"""
import sys, numpy as np
from PIL import Image
def run(src, dst, c):
    im = Image.open(src); mode = im.mode
    a = np.asarray(im.convert('RGBA')).astype(np.float32); H, W = a.shape[:2]
    W2 = W - c; out = a[:, :W2].copy()
    t = (np.arange(c) + .5) / c
    t = t * t * (3 - 2 * t)                       # smoothstep
    out[:, :c] = a[:, :c] * t[None, :, None] + a[:, W2:W] * (1 - t[None, :, None])
    o = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(o if mode == 'RGBA' else o[..., :3], 'RGBA' if mode == 'RGBA' else 'RGB').save(dst)
    print(dst, (W2, H))
if __name__ == '__main__':
    a = sys.argv[1:]; c = int(a[a.index('--c') + 1]) if '--c' in a else 300
    run(a[0], a[1], c)
