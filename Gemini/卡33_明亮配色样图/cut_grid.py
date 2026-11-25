# 把 Gemini 出的「浅灰底上一组角色」按连通块切开，去掉灰底，按行（上→下）、列（左→右）排序存成单张 PNG。
import sys, numpy as np, cv2
from PIL import Image
def cut(src, prefix, n_expect=6, rows=2, thr=26, min_area=20000):
    im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32); H, W = im.shape[:2]
    border = np.concatenate([im[:12].reshape(-1, 3), im[-12:].reshape(-1, 3), im[:, :12].reshape(-1, 3), im[:, -12:].reshape(-1, 3)])
    bg = np.median(border, 0)
    d = np.abs(im - bg).max(-1)
    fg = (d > thr).astype(np.uint8)
    # 背景 = 和画面边缘连通、接近底色的区域（洪水填充），被包住的小块底色只认非常接近底色的
    near = (d <= thr).astype(np.uint8)
    n, lab = cv2.connectedComponents(near, connectivity=4)
    edge_labels = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    bgmask = np.isin(lab, list(edge_labels))
    inner_bg = (d <= 8) & ~bgmask
    obj = (~bgmask & ~inner_bg).astype(np.uint8)
    obj = cv2.morphologyEx(obj, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    grp = cv2.morphologyEx(obj, cv2.MORPH_CLOSE, np.ones((41, 41), np.uint8))   # 同一个角色分开的零件（碎片、炮管）并成一组
    n, lab, st, cen = cv2.connectedComponentsWithStats(grp, 8)
    comps = [i for i in range(1, n) if st[i][4] > min_area]
    comps.sort(key=lambda i: (int(cen[i][1] // (H / rows)), cen[i][0]))
    out = []
    alpha_full = cv2.GaussianBlur(obj.astype(np.float32), (3, 3), 0)
    for k, i in enumerate(comps):
        x, y, w, h = st[i][:4]
        m = (lab[y:y + h, x:x + w] == i) & (obj[y:y + h, x:x + w] > 0)
        a = alpha_full[y:y + h, x:x + w] * (lab[y:y + h, x:x + w] == i)
        rgba = np.dstack([im[y:y + h, x:x + w], np.clip(a * 255, 0, 255)]).astype(np.uint8)
        p = '%s_%d.png' % (prefix, k); Image.fromarray(rgba, 'RGBA').save(p); out.append((p, (x, y, w, h)))
    print(len(out), 'pieces', [o[1] for o in out])
    return out
if __name__ == '__main__':
    cut(sys.argv[1], sys.argv[2])
