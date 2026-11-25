"""把 Gemini 样图（浅灰纯色底）按连通块切成透明 PNG：左边的角色、右边的单品。"""
import sys, json
import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage as ndi

def cut(path, out_prefix, thresh=16, merge=36, min_area=1500):
    im = Image.open(path).convert('RGB'); a = np.asarray(im).astype(np.int16)
    h, w, _ = a.shape
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    bg = np.median(border, axis=0)
    diff = np.abs(a - bg).max(axis=2)
    mask = diff > thresh
    mask = ndi.binary_closing(mask, iterations=3)
    mask = ndi.binary_fill_holes(mask)
    lab, n = ndi.label(mask)
    objs = ndi.find_objects(lab)
    boxes = []
    for i, sl in enumerate(objs):
        if sl is None: continue
        area = int((lab[sl] == i + 1).sum())
        if area < min_area: continue
        y0, y1, x0, x1 = sl[0].start, sl[0].stop, sl[1].start, sl[1].stop
        boxes.append([x0, y0, x1, y1, [i + 1], area])
    # 合并离得很近的块（光晕、气泡、分离的小零件）
    changed = True
    while changed:
        changed = False
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                A, B = boxes[i], boxes[j]
                if A[0] - merge < B[2] and B[0] - merge < A[2] and A[1] - merge < B[3] and B[1] - merge < A[3]:
                    boxes[i] = [min(A[0], B[0]), min(A[1], B[1]), max(A[2], B[2]), max(A[3], B[3]), A[4] + B[4], A[5] + B[5]]
                    boxes.pop(j); changed = True; break
            if changed: break
    rgba = np.dstack([np.asarray(im), np.zeros((h, w), np.uint8)])
    out = []
    for k, (x0, y0, x1, y1, ids, area) in enumerate(sorted(boxes, key=lambda b: (b[0] > w * .5, round(b[1] / (h * .3)), b[0]))):
        m = np.isin(lab[y0:y1, x0:x1], ids)
        alpha = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))
        crop = Image.fromarray(np.asarray(im)[y0:y1, x0:x1].copy()).convert('RGBA'); crop.putalpha(alpha)
        name = '%s_%d.png' % (out_prefix, k)
        crop.save(name)
        out.append({'file': name, 'box': [int(x0), int(y0), int(x1), int(y1)], 'area': area, 'cx': (x0 + x1) / 2 / w, 'cy': (y0 + y1) / 2 / h})
    return out

if __name__ == '__main__':
    res = {}
    for src, pre in (('cone.jpg', 'cone'), ('ghost.jpg', 'ghost'), ('coin.jpg', 'coin'), ('mix.jpg', 'mix')):
        res[pre] = cut(src, 'cut/' + pre)
    json.dump(res, open('cut/index.json', 'w'), indent=1)
    for k, v in res.items():
        print(k, [(o['file'].split('/')[-1], o['box'], round(o['cx'], 2), round(o['cy'], 2)) for o in v])
