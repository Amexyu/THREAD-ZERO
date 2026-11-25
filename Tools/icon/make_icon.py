#!/usr/bin/env python3
"""11-24 游戏图标（用户看了三轮例图，选了第三轮的 K：霓虹「0」+ 主角）。
主角从尾声插图（Art/V16/Ascension/epilogue.jpg）里按色相抠出来（青色背景去掉），站在雨夜墙上的橙色霓虹「0」（标题标志里的针眼）前面，义眼发光。
输出 ThreadZero/Assets/Icon/icon_{1024,512,256,128,64,48,32,16}.png。16 是只留霓虹「0」的简化版（主角缩到那么小看不清），其余都由 1024 缩小。
再在编辑器里点菜单「Thread Zero/检查/设置游戏图标（Assets/Icon）」设进 PlayerSettings。
主角头像来自 2048 宽的插图，放大到 1024 的图标会有点软；以后素材那边出了高分辨率头像，换掉 EPI / CROP 重新跑就行。
用法：python3 Tools/icon/make_icon.py [预览输出目录]（只要 Pillow）"""
import os, sys, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
RES = os.path.join(ROOT, 'ThreadZero', 'Assets', 'Resources')
EPI = os.path.join(RES, 'Art', 'V16', 'Ascension', 'epilogue.jpg')
OXA = os.path.join(RES, 'Fonts', 'Oxanium-Bold.ttf')
OUT = os.path.join(ROOT, 'ThreadZero', 'Assets', 'Icon')
SIZES = (1024, 512, 256, 128, 64, 48, 32, 16)
INK, ORANGE, TEAL = (14, 16, 18), (255, 74, 18), (64, 226, 204)
CROP = (780, 440, 1240, 900)                         # 插图里主角头和上身那一块
EYE, HEAD_C, HEAD_H, SEED = (301, 193), (225, 190), 200, (225, 190)   # 抠图里：义眼、头中心、头高（发梢到下巴）、取连通块的种子点

def cut_hero():
    """按色相把青色背景抠掉，只留和主角连在一起的那一块，补上里面的洞，边缘羽化一点。"""
    src = Image.open(EPI).convert('RGB').crop(CROP)
    h, s, v = src.convert('HSV').split()           # Pillow 的 HSV 都是 0–255：色相 140°–200° ≈ 99–142
    teal = ImageChops.multiply(ImageChops.multiply(h.point(lambda x: 255 if 99 <= x <= 142 else 0), s.point(lambda x: 255 if x > 76 else 0)), v.point(lambda x: 255 if x > 89 else 0))
    m = ImageChops.invert(teal).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    ImageDraw.floodfill(m, SEED, 128)
    keep = m.point(lambda x: 255 if x == 128 else 0)
    w, hh = keep.size
    inv = Image.new('L', (w + 2, hh + 2), 255); inv.paste(keep.point(lambda x: 0 if x else 255), (1, 1))
    ImageDraw.floodfill(inv, (0, 0), 128)            # 和边框连着的是背景，剩下没被填到的是洞
    filled = inv.point(lambda x: 0 if x == 128 else 255).crop((1, 1, w + 1, hh + 1))
    out = src.convert('RGBA'); out.putalpha(filled.filter(ImageFilter.GaussianBlur(1.2)))
    return out

def lin(W, top, bottom):
    g = Image.new('RGB', (1, 256))
    for y in range(256):
        u = y / 255; g.putpixel((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * u) for i in range(3)))
    return g.resize((W, W), Image.BILINEAR).convert('RGBA')

def glow(W, cx, cy, r, color, alpha, soft=.45):
    m = Image.new('L', (W, W), 0); R = W * r
    ImageDraw.Draw(m).ellipse([cx * W - R, cy * W - R, cx * W + R, cy * W + R], fill=int(255 * alpha))
    layer = Image.new('RGBA', (W, W), color + (0,)); layer.putalpha(m.filter(ImageFilter.GaussianBlur(R * soft))); return layer

def zero_mask(W, height_frac, cx, cy):
    probe = ImageFont.truetype(OXA, 1000); l, t, r, b = probe.getbbox('0', anchor='ls')
    f = ImageFont.truetype(OXA, int(1000 * W * height_frac / (b - t))); l, t, r, b = f.getbbox('0', anchor='ls')
    m = Image.new('L', (W, W), 0)
    ImageDraw.Draw(m).text((cx * W - (l + r) / 2, cy * W - (t + b) / 2), '0', font=f, fill=255, anchor='ls')
    return m

def neon(W, mask, color, width, glow_r):
    """霓虹灯管：沿 mask 的内外轮廓走一圈管子，外面两层光晕、中间一道亮芯。"""
    edge = ImageChops.subtract(mask.filter(ImageFilter.MaxFilter(int(width) | 1)), mask.filter(ImageFilter.MinFilter(int(width) | 1)))
    edge = edge.filter(ImageFilter.GaussianBlur(width * .12))
    out = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    for r, a in ((glow_r * 2.2, .35), (glow_r, .6)):
        g = Image.new('RGBA', (W, W), color + (0,)); g.putalpha(edge.filter(ImageFilter.GaussianBlur(r)).point(lambda v: min(255, int(v * a * 1.6)))); out.alpha_composite(g)
    tube = Image.new('RGBA', (W, W), color + (0,)); tube.putalpha(edge); out.alpha_composite(tube)
    core = edge.filter(ImageFilter.MinFilter(max(3, int(width * .45)) | 1))
    c = Image.new('RGBA', (W, W), tuple(min(255, int(v * .45 + 255 * .55)) for v in color) + (0,)); c.putalpha(core); out.alpha_composite(c)
    return out

def bricks(img, W):
    d = ImageDraw.Draw(img)
    for i in range(0, W, max(4, W // 26)): d.line([(0, i), (W, i)], fill=(26, 31, 36, 255), width=max(1, W // 300))

def finish(img, W):
    """圆角方块（四周留 3.5%），细亮边让深色任务栏上也有轮廓。"""
    p, rad = int(W * .035), int(W * .2)
    tm = Image.new('L', (W, W), 0); ImageDraw.Draw(tm).rounded_rectangle([p, p, W - p - 1, W - p - 1], radius=rad, fill=255)
    out = Image.new('RGBA', (W, W), (0, 0, 0, 0)); out.paste(img, (0, 0), tm)
    bl = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    ImageDraw.Draw(bl).rounded_rectangle([p, p, W - p - 1, W - p - 1], radius=rad, outline=(255, 255, 255, 30), width=max(1, int(W * .006)))
    out.alpha_composite(bl); return out

def hero_icon(W, hero_src):
    img = lin(W, (18, 22, 26), (6, 8, 10)); bricks(img, W)
    img.alpha_composite(glow(W, .5, .42, .48, ORANGE, .18))
    img.alpha_composite(neon(W, zero_mask(W, .68, .5, .40), (255, 96, 30), W * .03, W * .02))
    img.alpha_composite(glow(W, .45, .62, .30, TEAL, .22))
    s = .48 * W / HEAD_H
    hero = ImageEnhance.Sharpness(hero_src.resize((int(hero_src.size[0] * s), int(hero_src.size[1] * s)), Image.LANCZOS)).enhance(1.3)
    ox, oy = int(.47 * W - HEAD_C[0] * s), int(.62 * W - HEAD_C[1] * s)
    layer = Image.new('RGBA', (W, W), (0, 0, 0, 0)); layer.paste(hero, (ox, oy), hero)
    a = layer.split()[3].filter(ImageFilter.MaxFilter(int(W * .004) * 2 + 1)).filter(ImageFilter.GaussianBlur(W * .006))
    rim = Image.new('RGBA', (W, W), (255, 120, 60, 0)); rim.putalpha(a.point(lambda v: int(v * .6)))
    img.alpha_composite(rim); img.alpha_composite(layer)
    ex, ey = ox + EYE[0] * s, oy + EYE[1] * s; r0 = W * .021
    for rr, al, col in ((r0 * 2.6, .45, ORANGE), (r0 * 1.3, .8, (255, 120, 60)), (r0 * .45, 1, (255, 235, 210))):   # 义眼发光
        m = Image.new('L', (W, W), 0); ImageDraw.Draw(m).ellipse([ex - rr, ey - rr, ex + rr, ey + rr], fill=int(255 * al))
        l = Image.new('RGBA', (W, W), col + (0,)); l.putalpha(m.filter(ImageFilter.GaussianBlur(rr * .5))); img.alpha_composite(l)
    rng = random.Random(4); d = ImageDraw.Draw(img)
    for _ in range(70):   # 雨
        x = rng.uniform(0, W); y = rng.uniform(0, W); L = rng.uniform(W * .03, W * .08)
        d.line([(x, y), (x - L * .15, y + L)], fill=(200, 235, 240, rng.randint(16, 44)), width=max(1, int(W * .0022)))
    return finish(img, W)

def tiny_icon(W):
    """16 像素用：只留一个粗的霓虹「0」。"""
    img = lin(W, (22, 26, 30), (8, 10, 12))
    img.alpha_composite(glow(W, .5, .5, .42, ORANGE, .30))
    img.alpha_composite(neon(W, zero_mask(W, .78, .5, .5), (255, 100, 34), W * .085, W * .03))
    return finish(img, W)

if __name__ == '__main__':
    out_dir = sys.argv[1] if len(sys.argv) > 1 else OUT
    os.makedirs(out_dir, exist_ok=True)
    big = hero_icon(1024, cut_hero())
    tiny = tiny_icon(512)
    for size in SIZES:
        im = tiny.resize((size, size), Image.LANCZOS) if size <= 16 else (big if size == 1024 else big.resize((size, size), Image.LANCZOS))
        im.save(os.path.join(out_dir, 'icon_%d.png' % size))
        print('icon_%d.png' % size)
