"""地面带上下边缘的品红渗色：上下各 n 行里偏品红的像素，用同一列最近的干净像素替换。
用法: edgeclean.py in.png out.png [--n 6] [--thr 30]"""
import sys, numpy as np
from PIL import Image
def run(src, dst, n=6, thr=30):
    im = Image.open(src); mode = im.mode
    a = np.asarray(im.convert('RGBA')).astype(np.int32); H, W = a.shape[:2]
    mg = a[..., 0] + a[..., 2] - 2 * a[..., 1]
    bad = np.zeros((H, W), bool); bad[:n] = mg[:n] > thr; bad[H - n:] = mg[H - n:] > thr
    out = a.copy()
    for x in np.where(bad.any(0))[0]:
        col = bad[:, x]
        for y in range(n):
            if col[y]:
                yy = y
                while yy < H - 1 and col[yy]: yy += 1
                out[y, x] = a[yy, x]
        for y in range(H - 1, H - 1 - n, -1):
            if col[y]:
                yy = y
                while yy > 0 and col[yy]: yy -= 1
                out[y, x] = a[yy, x]
    o = out.astype(np.uint8)
    Image.fromarray(o if mode == 'RGBA' else o[..., :3], 'RGBA' if mode == 'RGBA' else 'RGB').save(dst, optimize=True)
    print('修了', int(bad.sum()), '个像素')
if __name__ == '__main__':
    a = sys.argv[1:]
    g = lambda k, d: type(d)(a[a.index(k) + 1]) if k in a else d
    run(a[0], a[1], g('--n', 6), g('--thr', 30))
