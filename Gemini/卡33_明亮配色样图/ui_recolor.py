# 明亮配色样图（整备页）：拿实机截图，按区域把深色块换成新配色，文字、卡片、角色一个像素都不动。
#   页面底色（整片深灰）→ 浅色底 + 淡花纹；侧边栏、属性面板、战力屏 → 各自的颜色（白字保留）。
import sys, numpy as np, cv2
from PIL import Image, ImageDraw

def hexrgb(h): return np.array([(h >> 16) & 255, (h >> 8) & 255, h & 255], np.float32)

PALETTES = {
    'A': dict(name='糖果街机', bg=0xD6F1FF, bg2=0xBFE6FB, side=0xFF6F61, sideSel=0xFF9A8F, stats=0x1A9E96, lcd=0x26285A, toast=0x26285A, pattern='dots'),
    'B': dict(name='晴天涂鸦', bg=0xFFE680, bg2=0xFFD84D, side=0x2F5BFF, sideSel=0x6C8BFF, stats=0xF2542D, lcd=0x1B2A78, toast=0x1B2A78, pattern='stripes'),
    'C': dict(name='柠檬汽水', bg=0xD9FBEA, bg2=0xC2F2DC, side=0x2FA4E7, sideSel=0x6CC3F2, stats=0x7B5CD6, lcd=0x24305E, toast=0x24305E, pattern='bubbles'),
}

def pattern_layer(W, H, p):
    img = Image.new('RGB', (W, H), tuple(int(v) for v in hexrgb(p['bg']))); d = ImageDraw.Draw(img); c2 = tuple(int(v) for v in hexrgb(p['bg2']))
    if p['pattern'] == 'dots':
        for y in range(0, H, 36):
            for x in range((y // 36 % 2) * 18, W, 36): d.ellipse((x - 4, y - 4, x + 4, y + 4), fill=c2)
    elif p['pattern'] == 'stripes':
        for k in range(-H, W, 56): d.polygon([(k, 0), (k + 26, 0), (k + 26 + H, H), (k + H, H)], fill=c2)
    else:
        rng = np.random.default_rng(3)
        for _ in range(420):
            x, y, r = rng.integers(0, W), rng.integers(0, H), int(rng.integers(5, 22)); d.ellipse((x - r, y - r, x + r, y + r), outline=c2, width=3)
    return np.asarray(img).astype(np.float32)

def tint_zone(out, im, rect, color, lo=.26, hi=.70):
    x0, y0, x1, y1 = rect; sub = im[y0:y1, x0:x1]; L = sub.mean(-1) / 255
    w = np.clip((L - lo) / (hi - lo), 0, 1); w = w * w * (3 - 2 * w)
    shade = hexrgb(color)[None, None] * (0.86 + 0.5 * np.clip(L / lo, 0, 1))[..., None]
    f = sub / 255; sat = f.max(-1) - f.min(-1)
    lift = (0.7 * np.clip(1 - sat * 5, 0, 1))[..., None]          # 灰白的字和图标提到接近纯白，彩色的小点不动
    kept = sub + (255 - sub) * lift
    out[y0:y1, x0:x1] = shade * (1 - w[..., None]) + kept * w[..., None]

def recolor_loadout(src, key, dst):
    p = PALETTES[key]; im = np.asarray(Image.open(src).convert('RGB')).astype(np.float32); H, W = im.shape[:2]
    out = im.copy(); f = im / 255; L = f.mean(-1); sat = f.max(-1) - f.min(-1)
    dark = ((L < 0.22) & (sat < 0.10)).astype(np.uint8)
    n, lab, stats, _ = cv2.connectedComponentsWithStats(dark, 8)
    outer = max(range(1, n), key=lambda i: stats[i][4] if stats[i][0] == 0 and stats[i][1] == 0 else 0)
    page = lab == outer
    side = (0, 0, 116, H); statsR = (152, 742, 636, 928); lcd = (430, 39, 614, 88)
    keep = np.zeros_like(page)
    for (x0, y0, x1, y1) in (side, statsR):
        keep[y0:y1, x0:x1] = True
    bgimg = pattern_layer(W, H, p)
    m = page & ~keep
    # 边缘软一点：深色区域往外 1 像素的半深像素按亮度混合
    soft = cv2.dilate(m.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & ~m & (L < 0.45) & (sat < 0.12)
    out[m] = bgimg[m]
    a = np.clip((0.45 - L[soft]) / 0.25, 0, 1)[..., None]; out[soft] = bgimg[soft] * a + im[soft] * (1 - a)
    tint_zone(out, im, side, p['side'])
    tint_zone(out, im, statsR, p['stats'])
    tint_zone(out, im, lcd, p['lcd'], lo=.18, hi=.5)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(dst); print(dst)

if __name__ == '__main__':
    recolor_loadout(sys.argv[1], sys.argv[2], sys.argv[3])
