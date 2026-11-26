# 卡47：套装单品图 → 抠洋红背景（只抠和画面边缘连着的背景，单品里面的粉色 / 紫色不会被抠掉）→ 按 3 行 × 5 列切成单件
import sys, os, json, numpy as np, cv2
from PIL import Image
def rgba(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = np.clip((np.minimum(r, b) - g - .6 * np.abs(r - b) - 40) / 60, 0, 1)
    # 只有和画面边缘连通的背景才算背景
    cand = (bg > .3).astype(np.uint8); n, lab = cv2.connectedComponents(cand, connectivity=4)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    outside = np.isin(lab, list(edge))
    # 背带、提手圈住的那一小块背景（不和边缘连通）：只认几乎纯洋红的（香水瓶这种粉紫色离纯洋红很远，不会被抠）
    d = np.abs(r - 255) + g + np.abs(b - 255)
    enc = ((~outside) & (d < 60)).astype(np.uint8); n2, lab2, st2, _ = cv2.connectedComponentsWithStats(enc, connectivity=4)
    big = [k for k in range(1, n2) if st2[k][4] >= 80]
    outside = outside | np.isin(lab2, big)
    near = cv2.dilate(outside.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    bg = np.where(outside, bg, np.where(near, np.minimum(bg, .5), 0)); al = 1 - bg
    col = np.clip((a - bg[..., None] * np.array([255, 0, 255], np.float32)) / np.maximum(al, 1e-3)[..., None], 0, 255)
    e = (al < .98) & (al > 0); gg = col[..., 1]
    col[..., 0] = np.where(e, np.minimum(col[..., 0], gg + 60), col[..., 0]); col[..., 2] = np.where(e, np.minimum(col[..., 2], gg + 60), col[..., 2])
    return np.dstack([col, al * 255]).astype(np.float32)
def cut(path, out_dir, rows=3, cols=5, min_area=3000):
    A = rgba(path); H, W = A.shape[:2]; m = (A[..., 3] > 100).astype(np.uint8)
    m2 = cv2.dilate(m, np.ones((21, 21), np.uint8))   # 同一件的碎块（警示灯的光线、兜帽绳子、毛衣针）连起来
    n, lab, st, cen = cv2.connectedComponentsWithStats(m2, 8)
    items = [dict(i=i, x=int(st[i][0]), y=int(st[i][1]), w=int(st[i][2]), h=int(st[i][3]), cx=float(cen[i][0]), cy=float(cen[i][1]), area=int(st[i][4])) for i in range(1, n) if st[i][4] >= min_area]
    # 按格子分：画面均分 rows × cols，每块归到中心所在的格子；同一格有几块就合并
    grid = {}
    for it in items:
        r = min(rows - 1, int(it['cy'] / H * rows)); c = min(cols - 1, int(it['cx'] / W * cols)); grid.setdefault((r, c), []).append(it)
    os.makedirs(out_dir, exist_ok=True); res = []
    for (r, c), its in sorted(grid.items()):
        keep = np.isin(lab, [it['i'] for it in its]) & (m > 0)
        ys, xs = np.nonzero(keep); pad = 6
        x0, y0, x1, y1 = max(0, xs.min() - pad), max(0, ys.min() - pad), min(W, xs.max() + pad + 1), min(H, ys.max() + pad + 1)
        piece = A[y0:y1, x0:x1].copy(); kk = np.isin(lab[y0:y1, x0:x1], [it['i'] for it in its]); piece[~kk, 3] = 0
        fn = 'r%d_c%d.png' % (r, c); Image.fromarray(np.clip(piece, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(out_dir, fn))
        res.append(dict(file=fn, row=r, col=c, box=[int(x0), int(y0), int(x1), int(y1)], parts=len(its)))
    json.dump(res, open(os.path.join(out_dir, 'pieces.json'), 'w'), indent=1)
    return res
if __name__ == '__main__':
    for src, dst in [('出图/套装1_雨天菜园码头_原图.jpeg', '切图/套装1'), ('出图/套装2_路锥信号鬼投币_原图.jpeg', '切图/套装2'), ('出图/套装3_回收社晚霞彩线_原图.jpeg', '切图/套装3')]:
        res = cut(src, dst); print(dst, len(res), [r['parts'] for r in res])
