# 卡42：紧身裤的大腿 / 小腿段（程序画的胶囊形：深灰底 + 黑网点 + 一道白条纹 + 粗黑描边），两端圆头，关节转动不露缝。
# 尺寸按标准图 A 量的（原图像素）：裤腿宽约 82，大腿 190，小腿 188。
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
SS = 4   # 超采样
def capsule(length, w_top, w_bot, stripe_at=.3, base=(58, 57, 58), outline=8, dots=True):
    r = max(w_top, w_bot) / 2; Wd = int(max(w_top, w_bot) + outline * 2 + 4); Ht = int(length + 2 * r + outline * 2 + 4)
    W2, H2 = Wd * SS, Ht * SS; cx = W2 / 2; y0 = (outline + 2 + r) * SS; y1 = y0 + length * SS
    def shape(grow):
        m = Image.new('L', (W2, H2), 0); d = ImageDraw.Draw(m)
        rt = (w_top / 2 + grow) * SS; rb = (w_bot / 2 + grow) * SS
        d.polygon([(cx - rt, y0), (cx + rt, y0), (cx + rb, y1), (cx - rb, y1)], fill=255)
        d.ellipse([cx - rt, y0 - rt, cx + rt, y0 + rt], fill=255); d.ellipse([cx - rb, y1 - rb, cx + rb, y1 + rb], fill=255)
        return np.asarray(m).astype(np.float32) / 255
    outer = shape(outline); inner = shape(0)
    img = np.zeros((H2, W2, 4), np.float32); img[..., 3] = outer * 255
    yy, xx = np.mgrid[0:H2, 0:W2]
    col = np.zeros((H2, W2, 3), np.float32); col[:] = base
    # 白条纹：在宽度的 stripe_at 处（偏后侧），宽约 0.18 个裤腿
    t = (yy - y0) / max(1, (y1 - y0)); half = ((w_top + (w_bot - w_top) * np.clip(t, 0, 1)) / 2) * SS
    rel = (xx - cx) / np.maximum(half, 1)   # -1..1
    stripe = (rel > -1 + 2 * stripe_at - .17) & (rel < -1 + 2 * stripe_at + .17)
    col[stripe] = (236, 236, 232)
    if dots:   # 网点：底色上黑点，条纹上灰点
        p = 7 * SS; gx = (xx % p) - p / 2; gy = ((yy + (xx // p % 2) * p / 2) % p) - p / 2; dd = np.sqrt(gx * gx + gy * gy)
        dot = dd < 1.6 * SS
        col[dot & ~stripe] = (24, 23, 25); col[dot & stripe] = (120, 120, 118)
    ink = (14, 12, 16)
    img[..., :3] = np.where(inner[..., None] > .5, col, ink)
    out = Image.fromarray(img.astype(np.uint8), 'RGBA').resize((Wd, Ht), Image.LANCZOS)
    top = (Wd / 2, outline + 2 + r); bot = (Wd / 2, outline + 2 + r + length)
    return out, top, bot
if __name__ == '__main__':
    im, t, b = capsule(190, 88, 80); im.save('/tmp/cap.png'); print(im.size, t, b)
