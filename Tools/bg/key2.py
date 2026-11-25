# 更稳的品红抠图（给带紫色的装备用）：原来的 key() 对每个像素都按「偏品红程度」压红蓝，紫色衣服会被洗成灰蓝、甚至变半透明。
# 这里只把「和画面边缘连通的品红」当背景（东西都有粗黑描边，洪水填充到描边就停），描边里面的颜色一个都不动；
# 被东西围住的小块背景（提手、天线之间）只认非常接近纯品红的。边缘半透明的像素按背景色反混合，去掉粉边。
import numpy as np
from PIL import Image, ImageFilter
from collections import deque

def _flood(cand):
    h, w = cand.shape; seen = np.zeros_like(cand); q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if cand[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if cand[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < h and 0 <= xx < w and cand[yy, xx] and not seen[yy, xx]: seen[yy, xx] = True; q.append((yy, xx))
    return seen

def key2(a, bg=None, lo=34.0, hi=110.0, pocket=26.0, step=2):
    """a: HxWx3 float（0–255）。返回 HxWx4 uint8。"""
    rgb = a[..., :3].astype(np.float32); H, W = rgb.shape[:2]
    if bg is None:   # 背景色取四条边上接近品红的像素的中位数（JPEG 的品红不是正好 255,0,255）
        edge = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
        mg = edge[(edge[:, 0] > 180) & (edge[:, 2] > 180) & (edge[:, 1] < 90)]
        bg = np.median(mg, 0) if len(mg) else np.array([255, 0, 255], np.float32)
    bg = np.asarray(bg, np.float32)
    d = np.sqrt(((rgb - bg) ** 2).sum(-1))
    # 低分辨率洪水填充（快），再放大回来
    ds = d[::step, ::step]; cand = ds < hi
    reach = _flood(cand)
    reach = np.kron(reach, np.ones((step, step), bool))[:H, :W]
    reach = np.asarray(Image.fromarray((reach * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3))) > 0   # 补齐放大时的锯齿
    reach &= d < hi
    pockets = d < pocket   # 被围住的背景小块：只认几乎纯品红
    bgm = reach | pockets
    # 边缘：只在背景和它往里 3 像素的带子里抠（和原来的 key() 同一个公式，久经考验）；带子里面的颜色一个都不动。
    band = np.asarray(Image.fromarray((bgm * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(7))) > 0
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    score = np.minimum(r, b) - g
    a_old = np.clip((190 - score) / 110, 0, 1); ex = np.clip(score, 0, None); kk = np.clip((score - 8) / 40, 0, 1)
    alpha = np.ones((H, W), np.float32); alpha[band] = a_old[band]
    alpha[pockets & ~reach] = 0
    fg = rgb.copy()
    fg[..., 0] = np.where(band, r - ex * kk, r); fg[..., 2] = np.where(band, b - ex * kk, b)
    fg = np.clip(fg, 0, 255)
    fg[alpha < 0.02] = (22, 20, 26)   # 全透明处填描边色：缩放时边缘不会混进品红
    out = np.dstack([fg, alpha * 255]).clip(0, 255).astype(np.uint8)
    return out

if __name__ == '__main__':
    import sys
    im = Image.open(sys.argv[1]).convert('RGB'); o = key2(np.asarray(im).astype(np.float32)); Image.fromarray(o, 'RGBA').save(sys.argv[2]); print(sys.argv[2])
