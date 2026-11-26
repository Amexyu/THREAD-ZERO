# 工具图：每行按 x 聚成 3 组（一组 = 一个型号，可能有好几个连通块，比如弹弓 + 气球），合成一块
import sys, os, json, numpy as np, cv2
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cut45 import rgba
def cut(path, out_dir, rows=4, cols=3, min_area=1500):
    A = rgba(path); H, W = A.shape[:2]; m = (A[..., 3] > 100).astype(np.uint8)
    n0, lab0, st0, _ = cv2.connectedComponentsWithStats(m, 8); lum = A[..., :3].mean(-1)
    for j in range(1, n0):
        x, y, w, h, area = st0[j]
        if h < 95 and area < 9000 and lum[lab0 == j].mean() < 70: m[lab0 == j] = 0; A[lab0 == j, 3] = 0   # Gemini 加的字
    n, lab, st, cen = cv2.connectedComponentsWithStats(cv2.dilate(m, np.ones((9, 9), np.uint8)), 8)
    comps = [dict(i=i, x=st[i][0], y=st[i][1], w=st[i][2], h=st[i][3], cx=cen[i][0], cy=cen[i][1]) for i in range(1, n) if st[i][4] >= min_area]
    comps = [c for c in comps if not (c['h'] < 120 and c['w'] > 2.0 * c['h'] and c['cx'] < .25 * W)]   # Gemini 自己加的行标题字
    ys = np.array([[c['cy']] for c in comps], np.float32)
    _, lb, ce = cv2.kmeans(ys, rows, None, (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, .5), 8, cv2.KMEANS_PP_CENTERS)
    rank = {int(c): r for r, c in enumerate(np.argsort(ce.ravel()))}
    os.makedirs(out_dir, exist_ok=True); res = []
    for r in range(rows):
        rc = [c for c, l in zip(comps, lb.ravel()) if rank[int(l)] == r]
        # 按列：图宽三等分（Gemini 排得很整齐）
        for k in range(cols):
            g = [c for c in rc if k * W / cols <= c['cx'] < (k + 1) * W / cols]
            if not g: continue
            x0 = max(0, min(c['x'] for c in g) - 6); y0 = max(0, min(c['y'] for c in g) - 6)
            x1 = min(W, max(c['x'] + c['w'] for c in g) + 6); y1 = min(H, max(c['y'] + c['h'] for c in g) + 6)
            piece = A[y0:y1, x0:x1].copy(); keep = np.isin(lab[y0:y1, x0:x1], [c['i'] for c in g]); piece[~keep, 3] = 0
            fn = 'r%d_c%d.png' % (r, k); Image.fromarray(np.clip(piece, 0, 255).astype(np.uint8), 'RGBA').save(os.path.join(out_dir, fn))
            res.append(dict(file=fn, row=r, col=k, box=[int(x0), int(y0), int(x1), int(y1)]))
    json.dump(res, open(os.path.join(out_dir, 'pieces.json'), 'w'), indent=1); return res
if __name__ == '__main__':
    res = cut(sys.argv[1], sys.argv[2]); print(len(res), [(r['row'], r['col']) for r in res])
