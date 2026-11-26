# 涂鸦贴图：从 Gemini 的品红底图里切出一个个图案；再按位置「喷」到图层的墙面上（透出砖缝）。
# 切：python3 decal.py cut <图.jpg> <输出前缀> [--ymin 0.4]     → 前缀_0.png …（按从左到右）
# 贴：python3 decal.py put <图层.png> <贴图.png> <中心 u> <中心 v> <高度(占图层高)> <输出.png> [--rot 度]
import sys, numpy as np, cv2
from PIL import Image
import os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'bg')); from key2 import key2   # Tools/bg/key2.py
cmd = sys.argv[1]
if cmd == 'cut':
    src, pre = sys.argv[2], sys.argv[3]; ymin = float(sys.argv[sys.argv.index('--ymin') + 1]) if '--ymin' in sys.argv else 0
    a = key2(np.asarray(Image.open(src).convert('RGB')).astype(np.float32))
    m = (a[..., 3] > 60).astype(np.uint8); m[:int(ymin * m.shape[0])] = 0
    m2 = cv2.dilate(m, np.ones((25, 25), np.uint8))
    n, lab, st, cen = cv2.connectedComponentsWithStats(m2, 8)
    comps = sorted([i for i in range(1, n) if st[i][4] > 20000], key=lambda i: st[i][0])
    for k, i in enumerate(comps):
        x, y, w, h = st[i][:4]
        piece = a[y:y + h, x:x + w].copy(); keep = (lab[y:y + h, x:x + w] == i)
        piece[..., 3] = np.where(keep, piece[..., 3], 0)
        Image.fromarray(piece, 'RGBA').save(f'{pre}_{k}.png'); print(f'{pre}_{k}.png', (w, h))
elif cmd == 'put':
    lay, dec, u, v, hh, out = sys.argv[2], sys.argv[3], float(sys.argv[4]), float(sys.argv[5]), float(sys.argv[6]), sys.argv[7]
    rot = float(sys.argv[sys.argv.index('--rot') + 1]) if '--rot' in sys.argv else 0
    L = Image.open(lay).convert('RGBA'); W, H = L.size
    D = Image.open(dec).convert('RGBA')
    th = int(hh * H); tw = int(D.width * th / D.height); D = D.resize((tw, th), Image.LANCZOS)
    if rot: D = D.rotate(rot, resample=Image.BICUBIC, expand=True)
    la = np.asarray(L).astype(np.float32); da = np.asarray(D).astype(np.float32)
    x0 = int(u * W - D.width / 2); y0 = int(v * H - D.height / 2)
    assert x0 >= 0 and x0 + D.width <= W, '贴图跨过了图层左右边（平铺接缝）'
    reg = la[y0:y0 + D.height, x0:x0 + D.width]
    g = cv2.cvtColor(reg[..., :3].astype(np.uint8), cv2.COLOR_RGB2GRAY).astype(np.float32) + 1
    hp = np.clip(g / (cv2.GaussianBlur(g, (0, 0), 5) + 1e-3), .78, 1.06)                      # 砖缝透出来
    al = da[..., 3:4] / 255 * (reg[..., 3:4] / 255)                                         # 只喷在墙上（图层透明处不喷）
    if '--trails' in sys.argv:                                                              # 墙上的流水痕迹从涂鸦上面流过去
        tm = np.load(sys.argv[sys.argv.index('--trails') + 1])[y0:y0 + D.height, x0:x0 + D.width]
        al = al * (1 - .7 * np.clip(tm, 0, 1)[..., None])
    paint = da[..., :3] * hp[..., None] * .96
    reg[..., :3] = reg[..., :3] * (1 - al) + paint * al
    la[y0:y0 + D.height, x0:x0 + D.width] = reg
    Image.fromarray(np.clip(la, 0, 255).astype(np.uint8), 'RGBA').save(out); print(out, 'at', (x0, y0), D.size)
