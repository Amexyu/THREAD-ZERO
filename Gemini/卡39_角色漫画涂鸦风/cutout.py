# 卡39：从「左正面 / 右侧面」设定图里把人抠出来（浅色网点底 → 透明），只用来拼例图，不进游戏。
# 用法：python3 cutout.py <原图> <输出前缀>   → <前缀>_front.png、<前缀>_side.png
import sys, numpy as np
from PIL import Image
from collections import deque
src, pre = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert('RGB')).astype(np.int16); H, W = a.shape[:2]
lum = a.mean(-1); chroma = a.max(-1) - a.min(-1)
light = (lum > 150) & (chroma < 40)                      # 浅色底（含网点之间的空白）
dark = lum < 110
# 地面虚线：下半部分里横向很多深色像素、竖向很薄的行
rows = dark[H // 2:].sum(1); gy = H // 2 + int(np.argmax(rows))
line = np.zeros_like(dark); line[max(0, gy - 6):gy + 7] = dark[max(0, gy - 6):gy + 7]
def half(x0, x1):
    m = light[:, x0:x1].copy(); h, w = m.shape
    bg = np.zeros((h, w), bool); q = deque()
    for y in range(h):
        for x in (0, w - 1):
            if m[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if m[y, x] and not bg[y, x]: bg[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not bg[yy, xx]: bg[yy, xx] = True; q.append((yy, xx))
    fg = ~bg; fg[line[:, x0:x1]] = False
    # 只留最大的一块（网点、虚线碎片都去掉）
    lab = np.zeros((h, w), np.int32); n = 0; sizes = []
    for y0, x0_ in zip(*np.nonzero(fg[::2, ::2])):
        y0 *= 2; x0_ *= 2
        if lab[y0, x0_] or not fg[y0, x0_]: continue
        n += 1; lab[y0, x0_] = n; q = deque([(y0, x0_)]); c = 1
        while q:
            y, x = q.popleft()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and fg[yy, xx] and not lab[yy, xx]: lab[yy, xx] = n; q.append((yy, xx)); c += 1
        sizes.append(c)
    keep = lab == (int(np.argmax(sizes)) + 1)
    rgba = np.dstack([a[:, x0:x1], keep * 255]).astype(np.uint8)
    im = Image.fromarray(rgba, 'RGBA'); bb = im.getchannel('A').getbbox(); return im.crop(bb), bb
f, fb = half(0, W // 2); s, sb = half(W // 2, W)
f.save(pre + '_front.png'); s.save(pre + '_side.png')
print(pre, 'ground y', gy, 'front', f.size, 'side', s.size, 'side bbox', sb)
