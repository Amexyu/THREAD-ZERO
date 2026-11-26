# 卡39：抠图后，被软管 / 背带围住的那块浅色网点底还留着（抠图的洪水填充进不去）。
# 找出抠图里「浅色、低饱和、里面撒着很多灰色小网点」的连通块，连同里面的网点一起去掉。T 恤、白鞋这种没有网点的浅色块不动。
import sys, numpy as np
from PIL import Image
from collections import deque
def comps(mask):
    H, W = mask.shape; lab = np.zeros((H, W), np.int32); out = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if lab[y0, x0]: continue
        k = len(out) + 1; lab[y0, x0] = k; q = deque([(y0, x0)]); pts = [(y0, x0)]
        while q:
            y, x = q.popleft()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < H and 0 <= xx < W and mask[yy, xx] and not lab[yy, xx]: lab[yy, xx] = k; q.append((yy, xx)); pts.append((yy, xx))
        out.append(pts)
    return lab, out
for p in sys.argv[1:]:
    im = Image.open(p).convert('RGBA'); a = np.asarray(im).copy(); H, W = a.shape[:2]
    rgb = a[..., :3].astype(np.int16); al = a[..., 3] > 0
    lum = rgb.mean(-1); chroma = rgb.max(-1) - rgb.min(-1)
    light = al & (lum > 200) & (chroma < 30)
    dotm = al & (lum < 200) & (lum > 60) & (chroma < 40)
    lab, cs = comps(light)
    dlab, ds = comps(dotm)
    small = np.zeros((H, W), bool)
    for pts in ds:
        if 2 <= len(pts) <= 70:
            ys, xs = zip(*pts); small[ys, xs] = True
    removed = 0
    for pts in cs:
        if len(pts) < 1500: continue
        ys, xs = np.array(pts).T; y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
        box_small = small[y0:y1 + 1, x0:x1 + 1].sum(); density = box_small / max(1, len(pts))
        if density > .04:   # 网点底：每 25 个浅色像素里至少有一个网点像素
            region = np.zeros((H, W), bool); region[ys, xs] = True
            # 把区域往外长 3 像素，包住网点
            g = region.copy()
            for _ in range(3):
                g2 = g.copy(); g2[1:] |= g[:-1]; g2[:-1] |= g[1:]; g2[:, 1:] |= g[:, :-1]; g2[:, :-1] |= g[:, 1:]; g = g2
            kill = region | (g & small)
            # 区域包围盒里的网点也一起去掉
            box = np.zeros((H, W), bool); box[y0:y1 + 1, x0:x1 + 1] = True; kill |= box & small
            a[kill, 3] = 0; removed += int(kill.sum())
            print(p, 'remove region', len(pts), 'density', round(density, 3), (int(x0), int(y0), int(x1), int(y1)))
    Image.fromarray(a, 'RGBA').save(p); print(p, 'removed px', removed)
