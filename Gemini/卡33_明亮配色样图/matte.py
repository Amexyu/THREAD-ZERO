# 黑底 / 白底两张实机截图 → 带透明度的前景（差值抠图）。
import sys, numpy as np
from PIL import Image
def matte(black, white, dst):
    B = np.asarray(Image.open(black).convert('RGB')).astype(np.float32)
    W = np.asarray(Image.open(white).convert('RGB')).astype(np.float32)
    a = 1 - (W - B).mean(-1) / 255.0
    a = np.clip(a, 0, 1)
    C = np.where(a[..., None] > 1e-3, B / np.maximum(a[..., None], 1e-3), 0)
    out = np.dstack([np.clip(C, 0, 255), a * 255]).astype(np.uint8)
    Image.fromarray(out, 'RGBA').save(dst); print(dst)
if __name__ == '__main__':
    matte(*sys.argv[1:4])
