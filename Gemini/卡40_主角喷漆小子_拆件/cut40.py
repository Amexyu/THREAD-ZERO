# 卡40：喷漆小子拆件图（品红底）→ 透明 PNG 部件。先列出连通块，再按位置认部件。
# 用法：python3 cut40.py <原图> <输出文件夹> [--list]
import sys, os, json, numpy as np, cv2
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
H, W = a.shape[:2]
r, g, b = a[..., 0], a[..., 1], a[..., 2]
m = np.minimum(r, b) - g; h = np.abs(r - b)
bg = np.clip((m - .6 * h - 40) / 60, 0, 1)            # 1 = 品红底
alpha = 1 - bg
k = np.maximum(alpha, 1e-3)[..., None]
col = np.clip((a - bg[..., None] * np.array([255, 0, 255], np.float32)) / k, 0, 255)   # 按品红反算回原色（去掉边上的粉）
edge = (alpha < .98) & (alpha > 0)
gg = col[..., 1]
col[..., 0] = np.where(edge, np.minimum(col[..., 0], gg + 60), col[..., 0])           # 边缘再压一下残留的品红
col[..., 2] = np.where(edge, np.minimum(col[..., 2], gg + 60), col[..., 2])
fg = (alpha > .5).astype(np.uint8)
n, lab, st, cen = cv2.connectedComponentsWithStats(fg, 8)
comps = []
for i in range(1, n):
    x, y, w, hh, area = st[i]
    if area < 400: continue
    comps.append(dict(i=i, x=int(x), y=int(y), w=int(w), h=int(hh), area=int(area), cx=float(cen[i][0]), cy=float(cen[i][1])))
comps.sort(key=lambda c: (round(c['cy'] / (H / 2.6)), c['cx']))
for c in comps: print(c)
np.save(os.path.join(out, '_lab.npy'), lab)
rgba = np.dstack([col, alpha * 255]).astype(np.uint8)
Image.fromarray(rgba).save(os.path.join(out, '_all.png'))
json.dump(comps, open(os.path.join(out, '_comps.json'), 'w'), ensure_ascii=False, indent=1)
