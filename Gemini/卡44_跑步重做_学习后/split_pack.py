# 身体图拆成「身体（不带背包）」+「背包」：背包本来就画在卫衣后面（卫衣后背的描边完整），所以拆开不会露空
import json, numpy as np, cv2
from PIL import Image
T = np.asarray(Image.open('部件A4/torso.png').convert('RGBA')).astype(np.float32); H, W = T.shape[:2]
r, g, b, a = [T[..., i] for i in range(4)]
yellow = (a > 128) & (r > 190) & (g > 150) & (b < 140) & (r - b > 80)
blue = (a > 60) & (b > r + 25) & (b > 120)
skin = (a > 60) & (r > 200) & (g > 150) & (b > 120) & (r > b + 30)
# 卫衣 = 黄色里最大的那一块（桶身上的小黄标不算）
ny, ly, sy, _ = cv2.connectedComponentsWithStats(yellow.astype(np.uint8), 8)
big = 1 + int(np.argmax(sy[1:, cv2.CC_STAT_AREA])); yellow = ly == big
pack = np.zeros((H, W), bool); edge = np.full(H, -1)
lims = np.array([(np.nonzero(yellow[y])[0].min() - 7) if yellow[y].any() else 104 for y in range(H)], float)
lims = np.minimum(lims, 104)
from scipy_free import medfilt
lims = medfilt(lims, 15)
for y in range(H):
    pack[y] = (a[y] > 8) & (np.arange(W) < lims[y]) & ~blue[y] & ~skin[y]
# 只要和橙色桶身连着的那一块（去掉卫衣下摆、短裤之类左边的零碎）
orange = (a > 128) & (r > 150) & (g < 150) & (b < 120) & (r - g > 70)
n, lab = cv2.connectedComponents(pack.astype(np.uint8), 8)
keep = set(np.unique(lab[orange & pack])) - {0}
pack = np.isin(lab, list(keep))
ys, xs = np.nonzero(pack); print('背包 bbox', xs.min(), xs.max(), ys.min(), ys.max(), '像素', pack.sum())
body = T.copy(); body[pack, 3] = 0
P = np.zeros_like(T); P[pack] = T[pack]
x0, x1, y0, y1 = max(0, xs.min() - 3), min(W, xs.max() + 4), max(0, ys.min() - 3), min(H, ys.max() + 4)
pk = P[y0:y1, x0:x1]
Image.fromarray(body.astype(np.uint8), 'RGBA').save('部件A4/torso_nopack.png'); Image.fromarray(pk.astype(np.uint8), 'RGBA').save('部件A4/pack_can.png')
# 挂点：背包右上角（肩带挂的地方）≈ 桶身右边缘、从上往下 30%
yy = int(ys.min() + .3 * (ys.max() - ys.min())); rx = xs[ys == yy].max() if (ys == yy).any() else xs.max()
piv_px = np.array([rx - x0, yy - y0], float); SC = 5.0
m = json.load(open('部件A4/parts.json')); tp = np.array(m['parts']['torso']['pivot'])          # 身体图的胯点（单位）
off = (np.array([rx, yy]) / SC) - tp                                                           # 挂点相对胯（单位）
print('pack pivot(单位)', (piv_px / SC).round(2), '相对胯', off.round(2))
json.dump(dict(pivot=(piv_px / SC).round(3).tolist(), off=off.round(3).tolist()), open('部件A4/pack_can.json', 'w'))
bg = lambda im: Image.alpha_composite(Image.new('RGBA', (im.shape[1], im.shape[0]), (40, 160, 255, 255)), Image.fromarray(im.astype(np.uint8), 'RGBA'))
v = np.concatenate([np.asarray(bg(body)), np.asarray(bg(P))], 1)
Image.fromarray(v).crop((0, 0, W * 2, 330)).resize((W * 4, 660), Image.NEAREST).save('out/_pack_split.png')
