# 卡45：品牌单品图 / 工具图 → 抠洋红背景、按连通块切成单件（按行、列排序），丢掉 Gemini 自己加的字
import sys, os, json, numpy as np, cv2
from PIL import Image
def rgba(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    bg = np.clip((np.minimum(r, b) - g - .6 * np.abs(r - b) - 40) / 60, 0, 1); al = 1 - bg
    col = np.clip((a - bg[..., None] * np.array([255, 0, 255], np.float32)) / np.maximum(al, 1e-3)[..., None], 0, 255)
    e = (al < .98) & (al > 0); gg = col[..., 1]
    col[..., 0] = np.where(e, np.minimum(col[..., 0], gg + 60), col[..., 0]); col[..., 2] = np.where(e, np.minimum(col[..., 2], gg + 60), col[..., 2])
    return np.dstack([col, al * 255]).astype(np.float32)
def cut(path, out_dir, rows, min_area=4000):
    A = rgba(path); H, W = A.shape[:2]; m = (A[..., 3] > 100).astype(np.uint8)
    # Gemini 自己加的字：小块、几乎全黑、矮 → 先去掉，免得膨胀以后粘到旁边的单品上
    n0, lab0, st0, _ = cv2.connectedComponentsWithStats(m, 8); lum = A[..., :3].mean(-1)
    for j in range(1, n0):
        x, y, w, h, area = st0[j]
        if h < 95 and area < 9000 and lum[lab0 == j].mean() < 70: m[lab0 == j] = 0; A[lab0 == j, 3] = 0
    m2 = cv2.dilate(m, np.ones((15, 15), np.uint8))                       # 同一件的碎块（鞋带、软管）连起来
    n, lab, st, cen = cv2.connectedComponentsWithStats(m2, 8)
    items = []
    for i in range(1, n):
        x, y, w, h, area = st[i]
        if area < min_area: continue
        sub = m[y:y + h, x:x + w] & (lab[y:y + h, x:x + w] == i)
        dens = sub.mean()
        if h < 60 and w > 1.6 * h: continue                               # 一行字
        items.append(dict(x=int(x), y=int(y), w=int(w), h=int(h), cx=float(x + w / 2), cy=float(y + h / 2), i=i, area=int(sub.sum())))
    # 按行分组（行数已知）：按 cy 排序后用 k-means 一维分 rows 组
    ys = np.array([it['cy'] for it in items]).reshape(-1, 1).astype(np.float32)
    _, labels, centers = cv2.kmeans(ys, rows, None, (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, .5), 5, cv2.KMEANS_PP_CENTERS)
    order = np.argsort(centers.ravel()); rank = {int(c): r for r, c in enumerate(order)}
    for it, l in zip(items, labels.ravel()): it['row'] = rank[int(l)]
    os.makedirs(out_dir, exist_ok=True); res = []
    for r in range(rows):
        row = sorted([it for it in items if it['row'] == r], key=lambda t: t['cx'])
        # 去掉行首的字（面积小、在最左边、而且比同行其他单品矮很多）
        if len(row) > 1:
            med = np.median([t['h'] for t in row])
            row = [t for t in row if not (t['h'] < .45 * med and t is row[0])]
        for c, it in enumerate(row):
            x, y, w, h = it['x'], it['y'], it['w'], it['h']; pad = 6
            x0, y0, x1, y1 = max(0, x - pad), max(0, y - pad), min(W, x + w + pad), min(H, y + h + pad)
            piece = A[y0:y1, x0:x1].copy(); keep = (lab[y0:y1, x0:x1] == it['i']); piece[~keep, 3] = 0
            fn = 'r%d_c%d.png' % (r, c); Image.fromarray(np.clip(piece, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(out_dir, fn))
            res.append(dict(file=fn, row=r, col=c, box=[int(x0), int(y0), int(x1), int(y1)]))
    json.dump(res, open(os.path.join(out_dir, 'pieces.json'), 'w'), indent=1)
    return res
if __name__ == '__main__':
    res = cut(sys.argv[1], sys.argv[2], int(sys.argv[3]))
    from collections import Counter
    print(len(res), Counter(r['row'] for r in res))
