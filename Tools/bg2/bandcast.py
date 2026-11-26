"""修正 tile.py 补出来的竖条的偏色：按行比较竖条核心和两侧邻近区域的中位色，平滑后加到竖条上（带羽化）。
用法: bandcast.py in.png out.png --json tile_in.json [--sigma 14] [--clip 22]"""
import sys, json, numpy as np, cv2
from PIL import Image
def run(src, dst, js, sigma=14, clip=22):
    j = json.load(open(js)); W0 = j['W']; x0, bw = j['x0'], j['bw']
    im = Image.open(src); has_a = im.mode == 'RGBA'
    a = np.asarray(im.convert('RGBA')).astype(np.float32)
    H, W = a.shape[:2]; assert W == W0
    half = W // 2
    b0 = (x0 - half) % W; b1 = (x0 + bw - half) % W          # 竖条在原图坐标：[b0, W) ∪ [0, b1)
    xs = np.arange(W)
    d_in = np.minimum((xs - b0) % W, (b1 - xs) % W)           # 竖条内：到竖条边的距离
    inside = ((xs - b0) % W) < bw
    core = inside & (d_in > 20)
    out_d = np.minimum((b0 - xs) % W, (xs - b1) % W)          # 竖条外：到竖条边的距离
    ref = (~inside) & (out_d > 40) & (out_d < 300)
    wgt = np.where(inside, 1.0, np.clip(1 - out_d / 60.0, 0, 1))
    rgb, al = a[..., :3], a[..., 3] / 255.
    delta = np.zeros((H, 3), np.float32); ok = np.zeros(H, np.float32)
    for y in range(H):
        mc = core & (al[y] > .9); mr = ref & (al[y] > .9)
        if mc.sum() > 30 and mr.sum() > 60:
            delta[y] = np.median(rgb[y, mr], 0) - np.median(rgb[y, mc], 0); ok[y] = 1
    # 平滑（只用有效行加权）
    k = cv2.getGaussianKernel(int(sigma * 6) | 1, sigma).ravel()
    num = np.stack([np.convolve(delta[:, c] * ok, k, 'same') for c in range(3)], 1)
    den = np.convolve(ok, k, 'same')[:, None]
    sm = np.where(den > .05, num / np.maximum(den, 1e-6), 0)
    sm = np.clip(sm, -clip, clip)
    rgb2 = rgb + sm[:, None, :] * wgt[None, :, None]
    o = np.concatenate([np.clip(rgb2, 0, 255), a[..., 3:]], 2).astype(np.uint8)
    Image.fromarray(o if has_a else o[..., :3], 'RGBA' if has_a else 'RGB').save(dst, optimize=True)
    rows = ok > 0
    print('竖条', b0, b1, '有效行', int(rows.sum()), '平均修正', np.round(sm[rows].mean(0), 1) if rows.any() else None)
if __name__ == '__main__':
    a = sys.argv[1:]
    g = lambda k, d: type(d)(a[a.index(k) + 1]) if k in a else d
    run(a[0], a[1], g('--json', ''), g('--sigma', 14.0), g('--clip', 22.0))
