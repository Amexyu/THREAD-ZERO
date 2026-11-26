"""透视地砖拉正（v0.37）：Gemini 常把“正面平视”的地砖画成一点透视（竖缝往中间收）。
在 y0..y1 这几行里，自动找上下两行的竖缝，拟合消失点，然后逐行横向缩放，让所有竖缝变成竖直、间距一致。
用法: persprect.py in.jpg out.png --rows y0-y1 [--ref y] [--scale 1.0]
  --rows：地砖所在的行（原图像素）；--ref：拉正后以哪一行的间距为准（默认最下面一行，顶部的行被放大）
  --scale：再整体乘一个系数（让地砖间距和栏杆之类的东西对上整数倍）"""
import sys, numpy as np, cv2
from PIL import Image
from scipy.signal import find_peaks
def lines(g, y, h=4):
    p = g[y - h:y + h].mean(0)
    base = cv2.GaussianBlur(p.reshape(1, -1), (0, 0), 15).ravel()
    pk, _ = find_peaks(base - p, height=20, distance=50)
    return pk.astype(np.float64)
def run(src, dst, y0, y1, ref=None, scale=1.0):
    a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32); H, W = a.shape[:2]
    g = a.mean(2)
    yt, yb = y0 + 12, y1 - 12
    lt, lb = lines(g, yt), lines(g, yb)
    # 按顺序配对：从两边往中间对齐数量
    n = min(len(lt), len(lb))
    best = None
    for off in range(0, abs(len(lt) - len(lb)) + 1):
        A = lt[off:off + n] if len(lt) > len(lb) else lt[:n]
        B = lb[off:off + n] if len(lb) > len(lt) else lb[:n]
        k, c = np.polyfit(A, B, 1)
        err = np.abs(B - (k * A + c)).mean()
        if best is None or err < best[0]: best = (err, k, c)
    err, k, c = best
    cx = c / (1 - k) if abs(1 - k) > 1e-6 else W / 2
    st = np.median(np.diff(lt)); sb = st * k
    print('消失中心 x=%.1f  上间距 %.1f  下间距 %.1f  拟合误差 %.2f' % (cx, st, sb, err))
    yr = y1 if ref is None else ref
    def sp(y): return st + (sb - st) * (y - yt) / (yb - yt)
    sref = sp(yr) * scale
    out = a.copy(); xs = np.arange(W, dtype=np.float32)
    for y in range(y0, y1 + 1):
        f = sp(y) / sref
        mapx = (cx + (xs - cx) * f).reshape(1, -1).astype(np.float32); mapy = np.full_like(mapx, y)
        out[y] = cv2.remap(a, mapx, mapy, cv2.INTER_CUBIC, borderMode=cv2.BORDER_REFLECT)[0]
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst)
    print(dst, '拉正后间距 %.1f' % sref)
if __name__ == '__main__':
    a = sys.argv[1:]
    y0, y1 = map(int, a[a.index('--rows') + 1].split('-'))
    ref = int(a[a.index('--ref') + 1]) if '--ref' in a else None
    sc = float(a[a.index('--scale') + 1]) if '--scale' in a else 1.0
    run(a[0], a[1], y0, y1, ref, sc)
