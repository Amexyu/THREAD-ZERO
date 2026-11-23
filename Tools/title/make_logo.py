#!/usr/bin/env python3
"""v0.22 标题标志：线缆织成的字。
一个橙色的「0」当针眼，几股青色的线从左边穿过它，织进游戏名的笔画里，再从右边垂出去。
四种语言各一张：ThreadZero/Assets/Resources/Art/V16/Title/logo_{zh,ja,en,ko}.png（1600×440，游戏里按 800×220 画）。
用法：python3 Tools/title/make_logo.py [预览底图输出目录]"""
import os, sys, math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
FONTS = os.path.join(ROOT, 'ThreadZero', 'Assets', 'Resources', 'Fonts')
OUT = os.path.join(ROOT, 'ThreadZero', 'Assets', 'Resources', 'Art', 'V16', 'Title')
W, H, SS = 1600, 440, 2            # 成品尺寸、超采样倍数
PAPER, TEAL, DEEP, ORANGE = (236, 233, 224), (64, 226, 204), (10, 44, 46), (255, 74, 18)
NAMES = {'zh': ('零织协议', 'NotoSansSC-Bold.otf', 250, 6), 'ja': ('零織プロトコル', 'NotoSansJP-Bold.otf', 168, 0),
         'ko': ('제로 위브 프로토콜', 'NotoSansKR-Bold.otf', 132, 0), 'en': ('THREAD ZERO', 'Oxanium-Bold.ttf', 176, 4)}

def make(lang):
    text, font_file, size, track = NAMES[lang]
    w, h = W * SS, H * SS
    rng = random.Random(7)
    font = ImageFont.truetype(os.path.join(FONTS, font_file), size * SS)
    zero_font = ImageFont.truetype(os.path.join(FONTS, 'Oxanium-Bold.ttf'), int(size * SS * (1.3 if lang != 'en' else 1.12)))

    # ---- 针眼「0」和字的位置 ----
    base = int(h * .5)
    zmask = Image.new('L', (w, h), 0); zd = ImageDraw.Draw(zmask)
    zx = 120 * SS
    zd.text((zx, base), '0', font=zero_font, fill=255, anchor='lm')
    zb = zmask.getbbox()
    tmask = Image.new('L', (w, h), 0); td = ImageDraw.Draw(tmask)
    x = zb[2] + 46 * SS
    for ch in text:
        td.text((x, base), ch, font=font, fill=255, anchor='lm')
        x += td.textlength(ch, font=font) + track * SS
    tb = tmask.getbbox()

    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    # ---- 字后面一层很淡的青色光 ----
    glow = tmask.filter(ImageFilter.GaussianBlur(14 * SS))
    img = Image.composite(Image.new('RGBA', (w, h), TEAL + (70,)), img, glow.point(lambda v: int(v * .5)))

    # ---- 线：三股青色的线从左边来，穿过针眼，从字的后面一路穿过去（字像串在线上），再从右边垂出去 ----
    d = ImageDraw.Draw(img, 'RGBA'); tpx = tmask.load(); zpx = zmask.load()
    th = tb[3] - tb[1]; zc = (tb[1] + tb[3]) // 2
    rows = [zc - int(th * .2), zc + int(th * .02), zc + int(th * .24)]
    tw = 5 * SS; ends = []
    def seg(x0, x1, y, alpha=235, over=False):
        if x1 <= x0: return
        if over: d.line([(x0, y), (x1, y)], fill=DEEP + (230,), width=tw + 4 * SS)
        d.line([(x0, y), (x1, y)], fill=TEAL + (alpha,), width=tw)
        d.line([(x0, y - SS), (x1, y - SS)], fill=(190, 255, 244, int(alpha * .6)), width=SS)   # 高光
    for ri, y in enumerate(rows):
        n = 40; xr = zb[0]
        for k in range(n): seg(xr * k / n, xr * (k + 1) / n + 1, y, int(235 * min(1, .08 + k / n)))   # 左边淡入
        last = max(xx for xx in range(tb[0], tb[2], SS) if tpx[xx, y] > 128) if any(tpx[xx, y] > 128 for xx in range(tb[0], tb[2], SS)) else tb[2]
        seg(xr, last, y)
        L = (150 + 70 * ri) * SS; sag = (26 + 22 * ri) * SS
        pts = [(last + L * u, y + sag * u * u) for u in [j / 30 for j in range(31)]]
        for j in range(30): d.line([pts[j], pts[j + 1]], fill=TEAL + (int(235 * (1 - j / 30) ** 1.2),), width=tw)
        ends.append(pts[14])

    # ---- 字：米白，一层很淡的横向织纹；深色描边压住后面的线 ----
    face = Image.new('RGBA', (w, h), PAPER + (255,)); fd = ImageDraw.Draw(face)
    for y in range(tb[1], tb[3], 9 * SS): fd.line([(0, y), (w, y)], fill=DEEP + (15,), width=1 * SS)
    edge = tmask.filter(ImageFilter.MaxFilter(3 * SS * 2 + 1))
    img = Image.composite(Image.new('RGBA', (w, h), DEEP + (245,)), img, edge)
    img = Image.composite(face, img, tmask)
    # ---- 橙色针眼：线从左半圈后面过去，洞里看得见，再压过右半圈出来 ----
    zedge = zmask.filter(ImageFilter.MaxFilter(3 * SS * 2 + 1))
    img = Image.composite(Image.new('RGBA', (w, h), DEEP + (245,)), img, zedge)
    img = Image.composite(Image.new('RGBA', (w, h), ORANGE + (255,)), img, zmask)
    d = ImageDraw.Draw(img, 'RGBA')
    for y in rows:
        x = zb[0]; inside = False; runs = []
        while x < zb[2] + 2:
            on = zpx[min(w - 1, x), y] > 128
            if on != inside: runs.append(x); inside = on
            x += 1
        if len(runs) >= 4: seg(runs[1] - 3 * SS, runs[2], y); seg(runs[2] - 1, runs[3] + 4 * SS, y, over=True)
    for ex, ey in ends:   # 线头
        r = 7 * SS; d.ellipse([ex - r, ey - r, ex + r, ey + r], fill=DEEP + (255,)); d.ellipse([ex - r + 2 * SS, ey - r + 2 * SS, ex + r - 2 * SS, ey + r - 2 * SS], fill=PAPER + (255,))
    return img.resize((W, H), Image.LANCZOS)

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for lang in NAMES:
        im = make(lang); p = os.path.join(OUT, 'logo_' + lang + '.png'); im.save(p); print(p, im.size)
        if len(sys.argv) > 1:
            bg = Image.new('RGB', (W, H), (22, 30, 36)); bg.paste(im, (0, 0), im); bg.save(os.path.join(sys.argv[1], 'logo_' + lang + '.jpg'), quality=90)
