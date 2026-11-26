# 身体图的下沿改成沿着短裤下摆那道粗黑边的下缘切（原来是一刀横切在黑边中间，还留了一截原图的腿）
import numpy as np, json, cv2
from PIL import Image
exec(open('build_strips.py', encoding='utf-8').read().split('# ---------- 腿 ----------')[0])
lum = A[..., :3].mean(-1); al = A[..., 3]
xsA = np.arange(200, 480); cut = np.full(len(xsA), np.nan)
for i, x in enumerate(xsA):
    run = 0
    for y in range(1145, 1080, -1):
        if al[y, x] < 128: run = 0; continue
        if lum[y, x] < 38: run += 1
        else: run = 0
        if run >= 5: cut[i] = y + 5; break      # 从下往上第一段连续 5 像素以上的实黑 = 下摆黑边，它的下缘
ok = ~np.isnan(cut); cut = np.interp(xsA, xsA[ok], cut[ok])
med = np.array([np.median(cut[max(0, i - 14):i + 15]) for i in range(len(cut))])   # 中值去掉腿描边那几列的误判
cut = np.convolve(np.pad(med, 6, mode='edge'), np.ones(13) / 13, 'valid')
print('下沿 y：', ' '.join('%d:%.0f' % (x, c) for x, c in zip(xsA[::20], cut[::20])))
np.save('out/_hem_cut.npy', np.stack([xsA, cut]))
pm = json.load(open('部件A4/parts.json')); piv = pm['parts']['torso']['pivot']; HIP = (370, 960)
T0 = np.asarray(Image.open('部件A4/torso.png').convert('RGBA')).astype(np.float32); th0, tw = T0.shape[:2]
EXT = 22; th = th0 + EXT
T = np.zeros((th, tw, 4), np.float32); T[:th0] = T0
# A → 身体贴图：tx = (x - 370) * K + piv_x * SC
M = np.array([[K, 0, -HIP[0] * K + piv[0] * SC], [0, K, -HIP[1] * K + piv[1] * SC]], np.float32)
Ahem = A.copy(); ya, xa = np.mgrid[0:A.shape[0], 0:A.shape[1]]
keep = (ya >= 1090) & (ya < np.interp(xa, xsA, cut)) & (xa > 200) & (xa < 480)
Ahem[~keep] = 0; Ahem[..., :3] *= Ahem[..., 3:4] / 255.
W_ = cv2.warpAffine(Ahem, M, (tw, th), flags=cv2.INTER_AREA, borderValue=0)
a_ = W_[..., 3:4] / 255.; rgb = np.where(a_ > 1e-3, W_[..., :3] / np.maximum(a_, 1e-3), 0)
base_a = T[..., 3:4] / 255.
out_a = a_ + base_a * (1 - a_); out_rgb = (rgb * a_ + T[..., :3] * base_a * (1 - a_)) / np.maximum(out_a, 1e-3)
T = np.dstack([out_rgb, out_a * 255])
Image.fromarray(T.astype(np.uint8), 'RGBA').save('部件A4/torso.png'); print('torso ok')
# 预览：原来和改后的下沿，放大 4 倍
o = Image.open('../卡42_主角标准站立图/部件A3/torso.png').convert('RGBA'); n = Image.open('部件A4/torso.png').convert('RGBA')
box = (0, 300, 269, 441); bg = lambda im: Image.alpha_composite(Image.new('RGBA', im.size, (255, 0, 255, 255)), im)
a = bg(o.crop(box)).resize((269 * 3, 141 * 3), Image.NEAREST); b = bg(n.crop(box)).resize((269 * 3, 141 * 3), Image.NEAREST)
s = Image.new('RGBA', (a.width, a.height * 2 + 10), (255, 255, 255, 255)); s.paste(a, (0, 0)); s.paste(b, (0, a.height + 10)); s.save('out/_hem_cmp.png')
