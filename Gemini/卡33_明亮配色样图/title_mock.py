# 明亮配色样图（标题画面）：Gemini 重画的亮色标题插画 + 游戏里的标志和菜单字（从实机截图里抠出来，换成深色）。
import sys, numpy as np
from PIL import Image, ImageFilter
INK = np.array([30, 34, 70], np.float32); INK2 = np.array([92, 98, 124], np.float32)
ORANGE = np.array([241, 90, 36], np.float32)
SCRIM = {'A': (255, 246, 240), 'B': (255, 250, 232), 'C': (244, 255, 248)}
LOGO_AT, LOGO_SIZE = (41, 101), (960, 264)            # 和游戏里一样（Ui 逻辑 34,84,800×220 × 1.2）
# 每一行字单独一个框，按框里最亮的字归一化（灰色小字也能抠满）。(x0, y0, x1, y1, 颜色) 颜色：0 深色主字、1 灰色副字、2 橙色
TEXT_BOXES = [(112, 346, 380, 384, 2), (112, 386, 570, 418, 1), (112, 516, 135, 630, 2), (140, 520, 270, 578, 0), (140, 578, 300, 603, 0),
              (140, 603, 490, 628, 1), (112, 652, 270, 815, 0), (1762, 1002, 1888, 1032, 1)]

def cover(img, W=1920, H=1080):
    k = max(W / img.width, H / img.height); im = img.resize((round(img.width * k), round(img.height * k)), Image.LANCZOS)
    x = (im.width - W) // 2; y = (im.height - H) // 2; return im.crop((x, y, x + W, y + H))

def recolor_logo(logo, mode):
    a = np.asarray(logo.convert('RGBA')).astype(np.float32); rgb, al = a[..., :3], a[..., 3:]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    orange = (r > 170) & (g < 150) & (b < 110)
    teal = (g > 110) & (b > 100) & (r < 140) & ~orange
    L = rgb.mean(-1, keepdims=True) / 255
    out = rgb.copy()
    neutral = ~orange & ~teal
    if mode == 'invert':                       # 白字 → 深色字，深色描线 → 浅色
        inv = INK[None, None] * L + np.array([250, 250, 252], np.float32) * (1 - L)
        out[neutral] = inv[neutral]
    out[teal] = np.array([24, 150, 140], np.float32)
    return Image.fromarray(np.dstack([out, al]).clip(0, 255).astype(np.uint8), 'RGBA')

def text_layer(cap):
    c = np.asarray(cap.convert('RGB')).astype(np.float32); H, W = c.shape[:2]
    lay = np.zeros((H, W, 4), np.float32)
    for x0, y0, x1, y1, kind in TEXT_BOXES:
        s = c[y0:y1, x0:x1]; r, g, b = s[..., 0], s[..., 1], s[..., 2]
        if kind == 2:   # 橙色字 / 选中条
            a = np.clip((r - g - 50) / 90, 0, 1) * (r > 140); col = ORANGE
        else:
            L = s.mean(-1); lo, hi = np.percentile(L, 30), np.percentile(L, 99.5)
            a = np.clip((L - lo - 12) / max(30, hi - lo - 24), 0, 1); col = INK if kind == 0 else INK2
        lay[y0:y1, x0:x1, :3] = col; lay[y0:y1, x0:x1, 3] = np.maximum(lay[y0:y1, x0:x1, 3], a * 255)
    return Image.fromarray(lay.clip(0, 255).astype(np.uint8), 'RGBA')

def compose(bg_path, cap_path, logo_path, key, dst, logo_mode='invert'):
    bg = cover(Image.open(bg_path).convert('RGB')).convert('RGBA')
    W, H = bg.size
    # 左边一层渐变的浅色薄纱，菜单字压在上面读得清
    x = np.arange(W, dtype=np.float32); t = np.clip(1 - x / 900, 0, 1); t = t * t * (3 - 2 * t)
    alpha = (0.76 * t)[None, :].repeat(H, 0)
    scr = np.zeros((H, W, 4), np.float32); scr[..., :3] = SCRIM[key]; scr[..., 3] = alpha * 255
    bg.alpha_composite(Image.fromarray(scr.astype(np.uint8), 'RGBA'))
    logo = recolor_logo(Image.open(logo_path), logo_mode).resize(LOGO_SIZE, Image.LANCZOS)
    bg.alpha_composite(logo, LOGO_AT)
    bg.alpha_composite(text_layer(Image.open(cap_path)))
    bg.convert('RGB').save(dst, quality=92); print(dst)

if __name__ == '__main__':
    a = sys.argv[1:]; compose(a[0], a[1], a[2], a[3], a[4], a[5] if len(a) > 5 else 'invert')
