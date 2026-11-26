# 身体图下沿：把短裤下摆下面突出来的窄小残块（原图腿的描边被一起切进来的部分）修掉，下沿按中值平滑
import numpy as np
from PIL import Image
T = np.asarray(Image.open('../卡42_主角标准站立图/部件A3/torso.png').convert('RGBA')).astype(np.float32); H, W = T.shape[:2]
a = T[..., 3]; prof = np.full(W, np.nan)
for x in range(W):
    rows = np.nonzero(a[360:, x] > 128)[0]
    if len(rows): prof[x] = 360 + rows.max()
ok = ~np.isnan(prof); xs = np.arange(W)
med = np.array([np.nanmedian(prof[max(0, i - 6):i + 7]) if ok[max(0, i - 6):i + 7].any() else np.nan for i in range(W)])
trim = 0
for x in range(W):
    if not ok[x] or np.isnan(med[x]): continue
    lim = med[x] + .5
    if prof[x] > lim + 1:                                   # 比周围低出一截 = 残块
        ys = np.arange(H) + .5; T[:, x, 3] *= np.clip(lim - ys + .5, 0, 1) * (ys > 360) + (ys <= 360); trim += 1
Image.fromarray(T.astype(np.uint8), 'RGBA').save('部件A4/torso.png'); print('修掉的列：', trim)
o = Image.open('../卡42_主角标准站立图/部件A3/torso.png').convert('RGBA'); n = Image.open('部件A4/torso.png').convert('RGBA')
bg = lambda im: Image.alpha_composite(Image.new('RGBA', im.size, (255, 0, 255, 255)), im)
box = (60, 370, 269, 419); a_ = bg(o.crop(box)).resize((209 * 4, 49 * 4), Image.NEAREST); b_ = bg(n.crop(box)).resize((209 * 4, 49 * 4), Image.NEAREST)
s = Image.new('RGBA', (a_.width, a_.height * 2 + 10), (255, 255, 255, 255)); s.paste(a_, (0, 0)); s.paste(b_, (0, a_.height + 10)); s.save('out/_hem_cmp.png')
