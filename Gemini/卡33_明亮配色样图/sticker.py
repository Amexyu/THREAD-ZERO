# 在 Gemini 出图上盖一张小贴纸（爱心 / 星星），用来盖掉贴纸上像文字的乱码。
# 用法: python3 sticker.py src.jpg dst.jpg x0 y0 x1 y1 [heart|star] [angle]
import sys, math
from PIL import Image, ImageDraw
def star_pts(cx, cy, r1, r2, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2; a = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts
def heart_pts(cx, cy, s):
    pts = []
    for i in range(80):
        t = 2 * math.pi * i / 80
        x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * s, cy - y * s))
    return pts
def sticker(im, box, kind='heart', angle=0):
    x0, y0, x1, y1 = box; w, h = x1 - x0, y1 - y0; S = 4
    lay = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    d.rounded_rectangle((2 * S, 2 * S, w * S - 2 * S, h * S - 2 * S), radius=min(w, h) * S // 4, fill=(255, 248, 228, 255), outline=(38, 38, 52, 255), width=3 * S)
    cx, cy = w * S / 2, h * S / 2; r = min(w, h) * S * 0.30
    if kind == 'star':
        d.polygon(star_pts(cx, cy, r, r * 0.45), fill=(255, 205, 60, 255), outline=(38, 38, 52, 255), width=2 * S)
    else:
        d.polygon(heart_pts(cx, cy + r * 0.1, r / 17), fill=(250, 110, 120, 255), outline=(38, 38, 52, 255), width=2 * S)
    lay = lay.resize((w, h), Image.LANCZOS)
    if angle: lay = lay.rotate(angle, resample=Image.BICUBIC, expand=True)   # 转了以后四个角不裁掉
    im.paste(lay, (int(round((x0 + x1) / 2 - lay.width / 2)), int(round((y0 + y1) / 2 - lay.height / 2))), lay)
    return im
if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]; box = tuple(int(v) for v in sys.argv[3:7])
    kind = sys.argv[7] if len(sys.argv) > 7 else 'heart'; ang = float(sys.argv[8]) if len(sys.argv) > 8 else 0
    im = Image.open(src).convert('RGB'); sticker(im, box, kind, ang); im.save(dst, quality=95)
