"""v0.29 例图皮肤：多层边、材质、底纹、贴纸、吊牌。坐标都是游戏逻辑坐标（1600×900），P() 换成超采样像素。
所有元件都画在「局部画布」上再贴到页面上（可以旋转），这样光晕、模糊只算小块，快很多。"""
from mk import *
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageFont
import math, random

# ---------- 颜色 ----------
PL_HI, PL, PL_LO, PL_EDGE = hx('FBFAF7'), hx('EEECE6'), hx('D9D6CE'), hx('B3AFA5')
PL_IN_TOP, PL_IN_BOT = hx('D3CFC6'), hx('E4E1DA')
INK2 = hx('17181A'); TXT = hx('24252A'); TXT2 = hx('6E6C67'); TXT3 = hx('9A978F')
OR, OR_D, OR_L = hx('FF4A12'), hx('C2340B'), hx('FF8A57')
RUB, RUB2 = hx('222326'), hx('34363A')
CARD_T, CARD_B = hx('2C2E33'), hx('131417')
SLOTC = [hx('FF7A3D'), hx('4FC3F7'), hx('9CCC65'), hx('BA8CFF'), hx('FFD54F')]   # 部位色（武 头 衣 背 腿）
QEN = ['STANDARD', 'IMPROVED', 'SPECIAL', 'MILSPEC', 'CLASSIFIED', 'PROTOTYPE', 'ZERO']

def C(c, a=None):
    c = tuple(c)
    if len(c) == 3: c = c + (255,)
    return c if a is None else c[:3] + (int(a),)

def mix(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))

# ---------- 局部画布 ----------
from contextlib import contextmanager
@contextmanager
def over(im):
    """半透明图形要先画在透明层上再叠上去（ImageDraw 直接画会把底下像素的透明度一起改掉）。"""
    lay = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    yield d
    im.alpha_composite(lay)

def canvas(w, h): return Image.new('RGBA', (max(1, P(w)), max(1, P(h))), (0, 0, 0, 0))

def put(img, piece, x, y, angle=0, alpha=1.0):
    """把局部画布贴到 (x, y)（逻辑坐标，左上角）；angle 绕中心旋转（度，逆时针为正）。"""
    if alpha < 1:
        a = piece.split()[3].point(lambda v: int(v * alpha)); piece = piece.copy(); piece.putalpha(a)
    if angle:
        cw, ch = piece.size
        piece = piece.rotate(angle, resample=Image.BICUBIC, expand=True)
        ox, oy = (piece.size[0] - cw) // 2, (piece.size[1] - ch) // 2
        img.alpha_composite(piece, (P(x) - ox, P(y) - oy))
    else:
        img.alpha_composite(piece, (P(x), P(y)))

def mask_round(size, r, inset=0):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, size[0] - 1 - inset, size[1] - 1 - inset], radius=max(0, r), fill=255)
    return m

def lglow(im, cx, cy, r, color, alpha=1.0, soft=.5):
    """局部光晕（在 im 自己的坐标里）。"""
    m = Image.new('L', im.size, 0)
    ImageDraw.Draw(m).ellipse([P(cx - r), P(cy - r), P(cx + r), P(cy + r)], fill=int(255 * alpha))
    m = m.filter(ImageFilter.GaussianBlur(max(1, P(r * soft))))
    l = Image.new('RGBA', im.size, C(color)[:3] + (0,)); l.putalpha(m); im.alpha_composite(l)

def lshadow(img, x, y, w, h, r=10, blur=10, alpha=.4, dy=6, dx=0, color=(0, 0, 0)):
    pad = blur * 3 + 4
    sh = canvas(w + pad * 2, h + pad * 2)
    ImageDraw.Draw(sh).rounded_rectangle(box(pad + dx, pad + dy, w, h), radius=P(r), fill=C(color, 255 * alpha))
    sh = sh.filter(ImageFilter.GaussianBlur(P(blur)))
    img.alpha_composite(sh, (P(x - pad), P(y - pad)))

def grad_img(size, top, bottom, horizontal=False):
    n = 256
    g = Image.new('RGBA', (1, n) if not horizontal else (n, 1))
    for i in range(n):
        u = i / (n - 1); c = tuple(int(top[k] + (bottom[k] - top[k]) * u) for k in range(4))
        g.putpixel((0, i) if not horizontal else (i, 0), c)
    return g.resize(size, Image.BILINEAR)

def fill_round(im, x, y, w, h, r, top, bottom=None, horizontal=False):
    """圆角填充（渐变可选），画在 im 上（逻辑坐标）。"""
    size = (max(1, P(w)), max(1, P(h)))
    g = grad_img(size, C(top), C(bottom if bottom is not None else top), horizontal)
    m = mask_round(size, P(r))
    g.putalpha(ImageChops.multiply(g.split()[3], m))
    im.alpha_composite(g, (P(x), P(y)))

def stroke_round(im, x, y, w, h, r, color, width):
    pad = width + 2
    lay = canvas(w + pad * 2, h + pad * 2)
    ImageDraw.Draw(lay).rounded_rectangle(box(pad, pad, w, h), radius=P(r), outline=C(color), width=max(1, P(width)))
    im.alpha_composite(lay, (P(x - pad), P(y - pad)))

def noise_overlay(im, x, y, w, h, r, strength=10, seed=1, streak=False):
    random.seed(seed)
    size = (max(1, P(w)), max(1, P(h)))
    small = Image.new('L', (max(1, size[0] // 3), max(1, size[1] // 3)))
    px = small.load()
    for j in range(small.size[1]):
        base = random.randint(-strength, strength) if streak else 0
        for i in range(small.size[0]):
            px[i, j] = 128 + (base if streak else random.randint(-strength, strength))
    n = small.resize(size, Image.BILINEAR)
    light = n.point(lambda v: max(0, v - 128) * 2); dark = n.point(lambda v: max(0, 128 - v) * 2)
    m = mask_round(size, P(r))
    for col, a in ((255, light), (0, dark)):
        l = Image.new('RGBA', size, (col, col, col, 0)); l.putalpha(ImageChops.multiply(a, m)); im.alpha_composite(l, (P(x), P(y)))

def halftone(im, x, y, w, h, color, step=9, rmax=2.6, origin='bl', reach=1.0, clip_r=0):
    """网点渐隐：从 origin 角往外越来越小。"""
    lay = canvas(w, h); d = ImageDraw.Draw(lay)
    ox, oy = {'bl': (0, h), 'tl': (0, 0), 'tr': (w, 0), 'br': (w, h)}[origin]
    diag = math.hypot(w, h) * reach
    j = 0; yy = 0
    while yy <= h + step:
        xx = (step / 2) if j % 2 else 0
        while xx <= w + step:
            t = 1 - math.hypot(xx - ox, yy - oy) / diag
            if t > 0:
                r = rmax * t
                if r > .25: d.ellipse(box(xx - r, yy - r, 2 * r, 2 * r), fill=C(color))
            xx += step
        yy += step * .87; j += 1
    if clip_r:
        lay.putalpha(ImageChops.multiply(lay.split()[3], mask_round(lay.size, P(clip_r))))
    im.alpha_composite(lay, (P(x), P(y)))

def hazard(im, x, y, w, h, c1=OR, c2=INK2, stripe=9, r=0):
    lay = canvas(w, h); d = ImageDraw.Draw(lay)
    d.rectangle([0, 0, lay.size[0], lay.size[1]], fill=C(c1))
    k = -h
    while k < w + h:
        d.polygon([(P(k), P(h)), (P(k + stripe), P(h)), (P(k + stripe + h), 0), (P(k + h), 0)], fill=C(c2))
        k += stripe * 2
    if r: lay.putalpha(ImageChops.multiply(lay.split()[3], mask_round(lay.size, P(r))))
    im.alpha_composite(lay, (P(x), P(y)))

def screw(im, cx, cy, r=4.2):
    d = ImageDraw.Draw(im)
    d.ellipse(box(cx - r - .8, cy - r - .8, 2 * r + 1.6, 2 * r + 1.6), fill=C(PL_EDGE))
    fill_round(im, cx - r, cy - r, 2 * r, 2 * r, r, hx('F4F3EF'), hx('B9B5AB'))
    d.line([P(cx - r * .6), P(cy + r * .35), P(cx + r * .6), P(cy - r * .35)], fill=C(hx('8A867D')), width=P(1.3))

def seam(im, x1, y1, x2, y2, dark=(0, 0, 0, 55), light=(255, 255, 255, 170)):
    if abs(y2 - y1) < .01:
        fill_round(im, min(x1, x2), y1 - .6, abs(x2 - x1), 1.2, .5, dark); fill_round(im, min(x1, x2), y1 + .8, abs(x2 - x1), 1, .5, light)
    else:
        fill_round(im, x1 - .6, min(y1, y2), 1.2, abs(y2 - y1), .5, dark); fill_round(im, x1 + .8, min(y1, y2), 1, abs(y2 - y1), .5, light)

def vents(im, x, y, n=5, w=30, gap=5, color=(0, 0, 0, 70)):
    for i in range(n):
        fill_round(im, x, y + i * gap, w, 2.2, 1.1, color)
        fill_round(im, x, y + i * gap + 2.2, w, 1, .5, (255, 255, 255, 140))

def barcode(im, x, y, w, h, color=INK2, seed=3):
    random.seed(seed); d = ImageDraw.Draw(im); xx = x
    while xx < x + w:
        bw = random.choice([.8, .8, 1.4, 2.2]); d.rectangle(box(xx, y, bw, h), fill=C(color)); xx += bw + random.choice([.8, 1.2, 1.8])

def otext(im, x, y, s, size, fill=hx('FFFFFF'), stroke=INK2, sw=1.6, kind='k', anchor='la', shadow=0):
    if shadow:
        text(im, x + shadow * .6, y + shadow, s, size, C(INK2, 120), kind, anchor, stroke=sw, stroke_fill=C(INK2, 120))
    text(im, x, y, s, size, C(fill), kind, anchor, stroke=sw, stroke_fill=C(stroke))

def spaced(im, x, y, s, size, fill, kind='n', spacing=.35, anchor_right=False):
    """间距英文。"""
    total = sum(tw(ch, size, kind) for ch in s) + spacing * size * (len(s) - 1)
    xx = x - total if anchor_right else x
    for ch in s:
        text(im, xx, y, ch, size, C(fill), kind); xx += tw(ch, size, kind) + spacing * size
    return total

# ---------- 材质面板 ----------
def plastic(img, x, y, w, h, r=16, top=PL_HI, bottom=PL_LO, rim=7, shadow_a=.38, dy=7, edge=PL_EDGE, noise=6):
    """白色家电塑料：投影 + 外缘 + 倒角厚度 + 渐变面 + 顶部高光线 + 细噪点。"""
    lshadow(img, x, y, w, h, r, blur=12, alpha=shadow_a, dy=dy)
    p = canvas(w, h)
    fill_round(p, 0, 0, w, h, r, edge, mix(C(edge), (0, 0, 0, 255), .25))          # 外缘（厚度的侧面）
    fill_round(p, 1.2, 1.2, w - 2.4, h - 2.4 - 2.5, r - 1, top, bottom)          # 正面
    fill_round(p, 1.2, 1.2, w - 2.4, 3, 1.5, (255, 255, 255, 230))                 # 顶边高光
    if noise: noise_overlay(p, 0, 0, w, h - 3, r, strength=noise, seed=int(x + y))
    img.alpha_composite(p, (P(x), P(y)))

def recess(img, x, y, w, h, r=12, top=PL_IN_TOP, bottom=PL_IN_BOT, depth=10):
    """凹进去的内腔：上沿内阴影更深。"""
    p = canvas(w, h)
    fill_round(p, 0, 0, w, h, r, top, bottom)
    sh = canvas(w, h); d = ImageDraw.Draw(sh)
    d.rounded_rectangle(box(-20, -20, w + 40, depth + 20), radius=P(r), fill=(0, 0, 0, 110))
    sh = sh.filter(ImageFilter.GaussianBlur(P(depth * .7)))
    sh.putalpha(ImageChops.multiply(sh.split()[3], mask_round(sh.size, P(r))))
    p.alpha_composite(sh)
    stroke_round(p, 0, 0, w, h, r, (0, 0, 0, 60), 1.3)
    fill_round(p, 1, h - 2.2, w - 2, 1.6, .8, (255, 255, 255, 150))
    img.alpha_composite(p, (P(x), P(y)))

def metal_dark(img, x, y, w, h, r=12, top=hx('303237'), bottom=hx('17181B'), shadow_a=.45):
    lshadow(img, x, y, w, h, r, blur=10, alpha=shadow_a, dy=6)
    p = canvas(w, h)
    fill_round(p, 0, 0, w, h, r, hx('0C0D0F'))
    fill_round(p, 1.5, 1.5, w - 3, h - 3, r - 1, top, bottom)
    noise_overlay(p, 1.5, 1.5, w - 3, h - 3, r - 1, strength=7, seed=int(x * 3 + y), streak=True)
    fill_round(p, 3, 1.5, w - 6, 1.4, .7, (255, 255, 255, 60))
    img.alpha_composite(p, (P(x), P(y)))

def keycap(img, x, y, w, h, label, kind='white', size=15, r=12, pressed=False, enabled=True, icon=None, sub=None, numeric=False):
    """带厚度的按键：white / orange / dark / ghost。"""
    faces = {'white': (PL_HI, hx('E2DFD8'), hx('B7B2A8'), TXT), 'orange': (hx('FF6A33'), hx('F0420C'), hx('A82A06'), hx('FFFFFF')),
             'dark': (hx('3A3C41'), hx('222327'), hx('0E0F11'), hx('F4F4F1')), 'ghost': (hx('E6E3DC'), hx('D8D4CB'), hx('B5B0A6'), TXT3)}
    if not enabled: kind = 'ghost'
    top, bot, side, tc = faces[kind]
    th = 0 if pressed else 4.5
    lshadow(img, x, y + (3 if pressed else 0), w, h - (3 if pressed else 0), r, blur=5, alpha=.32, dy=3)
    p = canvas(w, h)
    oy = 3 if pressed else 0
    fill_round(p, 0, oy, w, h - oy, r, side)
    fill_round(p, 1, oy + 1, w - 2, h - oy - 2 - th, r - 1, top, bot)
    fill_round(p, r * .6, oy + 1.6, w - r * 1.2, 2, 1, (255, 255, 255, 120 if kind != 'dark' else 40))
    img.alpha_composite(p, (P(x), P(y)))
    cy = y + oy + (h - oy - th) / 2
    if icon is not None:
        icon(img, x + 22, cy); tx = x + 40
        text(img, tx, cy, label, size, C(tc), 'k', 'lm')
    elif sub:
        text(img, x + w / 2, cy - size * .38, label, size, C(tc), 'k', 'mm'); text(img, x + w / 2, cy + size * .62, sub, size * .62, C(tc, 170), 'b', 'mm')
    else:
        text(img, x + w / 2, cy, label, size, C(tc), 'n' if numeric else 'k', 'mm')

# ---------- 小物件 ----------
def clip(img, cx, y, color=RUB, label='TZ'):
    """黑色塑料夹（夹在卡片上沿），上面竖印 TZ，顶上一个金属环。"""
    w, h = 22, 30
    p = canvas(w + 8, h + 12)
    d = ImageDraw.Draw(p)
    d.ellipse(box(4 + w / 2 - 5, 0, 10, 10), outline=C(hx('C9CCD0')), width=P(2))   # 金属环
    fill_round(p, 4, 6, w, h, 4, hx('3A3C40'), C(color))
    fill_round(p, 5, 7, w - 2, 2.2, 1, (255, 255, 255, 70))
    fill_round(p, 4 + w / 2 - 4, h - 2, 8, 6, 2, hx('17181A'))
    lab = canvas(h - 10, w - 6); text(lab, (h - 10) / 2, (w - 6) / 2, label, 8.5, C(hx('E8E8E4')), 'n', 'mm')
    lab = lab.rotate(90, expand=True); p.alpha_composite(lab, (P(4 + 3), P(6 + 4)))
    lshadow(img, cx - w / 2, y - 18, w, h, 4, blur=3, alpha=.35, dy=2)
    img.alpha_composite(p, (P(cx - w / 2 - 4), P(y - 24)))

def hook(img, cx, y):
    """挂杆下面的小白挂钩。"""
    fill_round(img, cx - 13, y, 26, 10, 3, hx('F7F6F2'), hx('CFCBC2'))
    stroke_round(img, cx - 13, y, 26, 10, 3, (0, 0, 0, 60), 1)

def rail(img, x, y, w, t=8):
    """镀铬横杆 + 两端支架。"""
    lshadow(img, x, y, w, t, t / 2, blur=4, alpha=.35, dy=5)
    p = canvas(w, t)
    g = grad_img((P(w), P(t)), C(hx('FFFFFF')), C(hx('8F949B')))
    g2 = grad_img((P(w), P(t)), C(hx('E9ECEF')), C(hx('6F747B')))
    m = mask_round((P(w), P(t)), P(t / 2)); p.alpha_composite(g); band = canvas(w, t * .45); band.alpha_composite(g2.crop((0, 0, P(w), P(t * .45))))
    p.alpha_composite(band, (0, P(t * .55)))
    p.putalpha(ImageChops.multiply(p.split()[3], m))
    img.alpha_composite(p, (P(x), P(y)))
    for ex in (x - 6, x + w - 8):
        fill_round(img, ex, y - 6, 14, t + 12, 3, hx('F2F0EB'), hx('BDB8AE')); stroke_round(img, ex, y - 6, 14, t + 12, 3, (0, 0, 0, 70), 1)
        screw(img, ex + 7, y + t / 2, 3)

def sticker_round(img, cx, cy, r, fill, label, angle=0, size=None, tc=hx('FFFFFF'), kind='n'):
    p = canvas(r * 2 + 8, r * 2 + 8); d = ImageDraw.Draw(p)
    d.ellipse(box(4, 4 + 1.5, 2 * r, 2 * r), fill=(0, 0, 0, 70))
    d.ellipse(box(4, 4, 2 * r, 2 * r), fill=C(hx('FFFFFF')))
    d.ellipse(box(6, 6, 2 * r - 4, 2 * r - 4), fill=C(fill))
    text(p, r + 4, r + 4, label, size or r * .8, C(tc), kind, 'mm')
    put(img, p, cx - r - 4, cy - r - 4, angle)

def tag_sticker(img, x, y, w, h, fill, label, angle=0, size=10, tc=INK2):
    p = canvas(w + 6, h + 6)
    fill_round(p, 3, 4, w, h, 3, (0, 0, 0, 80))
    fill_round(p, 2, 2, w + 2, h + 2, 4, hx('FFFFFF'))
    fill_round(p, 3, 3, w, h, 3, fill)
    text(p, 3 + w / 2, 3 + h / 2, label, size, C(tc), 'k', 'mm')
    put(img, p, x - 3, y - 3, angle)

def tape(img, x, y, w, h, angle=0, color=(255, 246, 214, 150), seed=5):
    random.seed(seed)
    p = canvas(w, h); d = ImageDraw.Draw(p)
    pts = [(0, 0)]
    for k in range(1, 7): pts.append((P(w * k / 6), P(random.uniform(0, 1.6))))
    for k in range(1, 7): pts.append((P(w - random.uniform(0, 2.2)), P(h * k / 6)))
    for k in range(5, -1, -1): pts.append((P(w * k / 6), P(h - random.uniform(0, 1.6))))
    for k in range(5, 0, -1): pts.append((P(random.uniform(0, 2.2)), P(h * k / 6)))
    d.polygon(pts, fill=C(color))
    put(img, p, x, y, angle)

def stamp(img, cx, cy, label, color=OR, angle=-12, size=14, w=None):
    w = w or tw(label, size, 'k') + 26; h = size + 16
    p = canvas(w + 8, h + 8); d = ImageDraw.Draw(p)
    d.rounded_rectangle(box(4, 4, w, h), radius=P(5), outline=C(color, 220), width=P(2.4))
    d.rounded_rectangle(box(7, 7, w - 6, h - 6), radius=P(3), outline=C(color, 170), width=P(1))
    text(p, 4 + w / 2, 4 + h / 2, label, size, C(color, 230), 'k', 'mm')
    random.seed(int(cx * 7 + cy))                      # 印章斑驳：随机擦掉一些像素
    a = p.split()[3]; px = a.load()
    for _ in range(int(p.size[0] * p.size[1] * .06)):
        i, j = random.randrange(p.size[0]), random.randrange(p.size[1]); px[i, j] = int(px[i, j] * .25)
    p.putalpha(a.filter(ImageFilter.GaussianBlur(.6)))
    put(img, p, cx - w / 2 - 4, cy - h / 2 - 4, angle)

def led(img, x, y, w, color=OR, t=4):
    lay = canvas(w + 40, t + 40)
    fill_round(lay, 20, 20, w, t, t / 2, C(color))
    lay = lay.filter(ImageFilter.GaussianBlur(P(6)))
    img.alpha_composite(lay, (P(x - 20), P(y - 20)))
    fill_round(img, x, y, w, t, t / 2, mix(C(color), (255, 255, 255, 255), .35))

def lcd(img, x, y, w, h, value, label=None, color=OR, size=20):
    fill_round(img, x, y, w, h, 6, hx('101113'))
    stroke_round(img, x, y, w, h, 6, (255, 255, 255, 40), 1)
    fill_round(img, x + 3, y + 3, w - 6, h * .42, 4, (255, 255, 255, 14))
    t = canvas(w, h); text(t, w - 10, h / 2, value, size, C(color), 'n', 'rm')
    g = t.filter(ImageFilter.GaussianBlur(P(2.5))); img.alpha_composite(g, (P(x), P(y))); img.alpha_composite(t, (P(x), P(y)))
    if label: text(img, x + 10, y + h / 2, label, 10, C(hx('8E8F8D')), 'b', 'lm')

# ---------- 装备卡 ----------
EFFECT_GLYPH = {'处决': '决', '电弧': '弧', '汲取': '汲', '标记': '标', '护盾': '盾', '无人机': '机', '推进': '推'}
SLOT_GLYPH = ['武', '头', '衣', '背', '腿']

def badge(p, cx, cy, r, col, glyph, tc=INK2):
    with over(p) as d: d.ellipse(box(cx - r - 1.6, cy - r - 1.6 + 1.2, 2 * r + 3.2, 2 * r + 3.2), fill=(0, 0, 0, 120))
    fill_round(p, cx - r - 1.6, cy - r - 1.6, 2 * r + 3.2, 2 * r + 3.2, r + 1.6, INK2)
    fill_round(p, cx - r, cy - r, 2 * r, 2 * r, r, mix(C(col), (255, 255, 255, 255), .25), C(col))
    with over(p) as d: d.ellipse(box(cx - r + 1.4, cy - r + 1.4, 2 * r - 2.8, 2 * r - 2.8), outline=(255, 255, 255, 110), width=P(.9))
    text(p, cx, cy + .4, glyph, r * 1.08, C(tc), 'k', 'mm')

def gem(p, cx, cy, r, fill, on=True):
    d = ImageDraw.Draw(p)
    pts = [(P(cx), P(cy - r - 1.3)), (P(cx + r + 1.3), P(cy)), (P(cx), P(cy + r + 1.3)), (P(cx - r - 1.3), P(cy))]
    d.polygon(pts, fill=C(INK2))
    pts = [(P(cx), P(cy - r)), (P(cx + r), P(cy)), (P(cx), P(cy + r)), (P(cx - r), P(cy))]
    d.polygon(pts, fill=C(fill) if on else C(hx('3E4045')))
    if on:
        with over(p) as d2: d2.polygon([(P(cx), P(cy - r)), (P(cx + r), P(cy)), (P(cx), P(cy))], fill=(255, 255, 255, 90))

def icon_fit(icon, w, h, frac=.84):
    s = min(w * frac / icon.size[0], h * frac / icon.size[1]) * SS
    return icon.resize((max(1, int(icon.size[0] * s)), max(1, int(icon.size[1] * s))), Image.LANCZOS)

def silhouette(icon, color):
    sil = Image.new('RGBA', icon.size, C(color)); sil.putalpha(icon.split()[3]); return sil

def gear_card(img, x, y, w, h, icon, q, name, lv, power=None, effect=None, slot=0, model='', equipped=False, new=False,
              selected=False, small=False, angle=0, owned=True, current=False, locked=False, sub=None, art_ratio=None, name_size=None, foot=None):
    """v0.29 装备卡：外黑边 + 品质内边 + 内高光；画框里有品质光、放射光、网点；右侧竖条印品质英文；下面名字、型号、品质宝石、战力、等级。"""
    qc = C(Q[q]); pad = 14
    if selected:
        y -= 8
        R = max(w, h) * .5; g = R * 1.5; gl = canvas(w + g * 2, h + g * 2); lglow(gl, w / 2 + g, h / 2 + g, R, OR, .5, .4); put(img, gl, x - g, y - g, angle)
    p = canvas(w + pad * 2, h + pad * 2 + 20)
    ox, oy = pad, pad + 20
    # 投影
    sh = canvas(w + pad * 2, h + pad * 2 + 20)
    ImageDraw.Draw(sh).rounded_rectangle(box(ox + 1, oy + 6, w - 2, h), radius=P(9), fill=(0, 0, 0, 150 if not selected else 120))
    sh = sh.filter(ImageFilter.GaussianBlur(P(6 if not small else 4))); p.alpha_composite(sh)
    # 外黑边 + 品质内边（上亮下暗）
    fill_round(p, ox, oy, w, h, 9, hx('0A0B0C'))
    fill_round(p, ox + 1.8, oy + 1.8, w - 3.6, h - 3.6, 8, mix(qc, (255, 255, 255, 255), .25), mix(qc, (0, 0, 0, 255), .45))
    fill_round(p, ox + 3.8, oy + 3.8, w - 7.6, h - 7.6, 6.5, CARD_T, CARD_B)
    # 右侧品质竖条（印品质英文）
    sw = 7 if small else 10
    sx = ox + w - 3.8 - sw
    fill_round(p, sx, oy + 3.8, sw, h - 7.6, 3, mix(qc, (255, 255, 255, 255), .1), mix(qc, (0, 0, 0, 255), .5))
    if not small:
        lab = canvas(h - 30, sw); spaced(lab, 4, (sw - 7.5) / 2 - .5, QEN[q], 7.5, C(INK2, 150), 'n', .5)
        lab = lab.rotate(-90, expand=True); p.alpha_composite(lab, (P(sx), P(oy + 18)))
    # 画框
    ax, ay, aw, ah = ox + 7, oy + 8, w - sw - 14.5, h * (art_ratio if art_ratio else (.54 if not small else .63))
    art = canvas(aw, ah)
    fill_round(art, 0, 0, aw, ah, 5, hx('15161A'), hx('0B0C0E'))
    if owned:
        lglow(art, aw / 2, ah * .62, aw * .55, qc, .42, .55)
        rays = canvas(aw, ah); dr = ImageDraw.Draw(rays)
        for k in range(5):
            a0 = -1.9 + k * .32; cx0, cy0 = aw * .5, ah * 1.05
            dr.polygon([(P(cx0), P(cy0)), (P(cx0 + math.cos(a0) * ah * 2), P(cy0 + math.sin(a0) * ah * 2)),
                        (P(cx0 + math.cos(a0 + .1) * ah * 2), P(cy0 + math.sin(a0 + .1) * ah * 2))], fill=qc[:3] + (26,))
        art.alpha_composite(rays)
        halftone(art, 0, 0, aw, ah, qc[:3] + (60,), step=6.5, rmax=1.5, origin='br', reach=.8)
        if q >= 4:
            sheen = canvas(aw, ah); ds = ImageDraw.Draw(sheen)
            for k in range(3):
                sx0 = aw * (.12 + k * .3)
                ds.polygon([(P(sx0), 0), (P(sx0 + 9), 0), (P(sx0 - 14), P(ah)), (P(sx0 - 23), P(ah))], fill=(255, 255, 255, 22))
            art.alpha_composite(sheen)
        if q >= 6:
            random.seed(q * 31 + int(aw))
            for _ in range(7):
                sx1, sy1, rr = random.uniform(6, aw - 6), random.uniform(6, ah - 6), random.uniform(1.2, 2.6)
                with over(art) as d2: d2.polygon([(P(sx1), P(sy1 - rr * 2)), (P(sx1 + rr * .5), P(sy1)), (P(sx1), P(sy1 + rr * 2)), (P(sx1 - rr * .5), P(sy1))], fill=(255, 255, 255, 200))
                with over(art) as d2: d2.polygon([(P(sx1 - rr * 2), P(sy1)), (P(sx1), P(sy1 - rr * .5)), (P(sx1 + rr * 2), P(sy1)), (P(sx1), P(sy1 + rr * .5))], fill=(255, 255, 255, 200))
    ic = icon if owned else silhouette(icon, hx('44474D'))
    ic2 = icon_fit(ic, aw, ah, .86)
    if owned:
        dsh = Image.new('RGBA', ic2.size, (0, 0, 0, 0)); dsh.putalpha(ic2.split()[3].point(lambda v: int(v * .55)))
        dsh = dsh.filter(ImageFilter.GaussianBlur(P(2))); art.alpha_composite(dsh, (P(aw / 2) - ic2.size[0] // 2 + P(2), P(ah / 2) - ic2.size[1] // 2 + P(3)))
    art.alpha_composite(ic2, (P(aw / 2) - ic2.size[0] // 2, P(ah / 2) - ic2.size[1] // 2))
    if not owned:
        cx, cy = aw / 2, ah / 2
        fill_round(art, cx - 12, cy - 3, 24, 19, 4, hx('8A8D93'), hx('5D6066'))
        ImageDraw.Draw(art).arc(box(cx - 8.5, cy - 16, 17, 22), 180, 360, fill=C(hx('8A8D93')), width=P(3.2))
    # 画框内阴影 + 描边
    ins = canvas(aw, ah); ImageDraw.Draw(ins).rounded_rectangle(box(-10, -10, aw + 20, 18), radius=P(5), fill=(0, 0, 0, 120))
    ins = ins.filter(ImageFilter.GaussianBlur(P(4))); ins.putalpha(ImageChops.multiply(ins.split()[3], mask_round(ins.size, P(5)))); art.alpha_composite(ins)
    stroke_round(art, 0, 0, aw, ah, 5, (255, 255, 255, 30), 1)
    art.putalpha(ImageChops.multiply(art.split()[3], mask_round(art.size, P(5))))
    p.alpha_composite(art, (P(ax), P(ay)))
    # 角标
    br = 8.5 if not small else 6.6
    bx, by = ax + aw - br - 3.5, ay + br + 3.5
    if effect: badge(p, bx, by, br, qc, EFFECT_GLYPH.get(effect, '·')); by += br * 2 + 4
    badge(p, bx, by, br, hx('E9E9E4'), SLOT_GLYPH[slot])
    # 名字（左边部位色小条）+ 型号
    ny = ay + ah + (6 if not small else 4)
    fs = name_size or (12.5 if not small else 10)
    maxw = w - sw - 22
    while tw(name, fs, 'k') > maxw and fs > 7: fs -= .5
    fill_round(p, ox + 6.5, ny + 1.5, 3, fs + 1, 1.5, SLOTC[slot])
    otext(p, ox + 13, ny, name, fs, hx('FFFFFF') if owned else hx('9A9DA3'), sw=1.5)
    gy = ny + fs + (8 if not small else 5)
    if sub:
        text(p, ox + 13, gy - 3, sub, 9.5, C(hx('A4A8AE') if owned else hx('7B7F86')), 'r'); gy += 13
    elif model and not small:
        mfs = 6.8
        while spaced(canvas(1, 1), 0, 0, model, mfs, (0, 0, 0, 0), 'n', .25) > maxw - 4 and mfs > 5: mfs -= .3
        spaced(p, ox + 13, gy - 2, model, mfs, C(hx('8C9096')), 'n', .25); gy += 9
    for i in range(7): gem(p, ox + 14 + i * (9.6 if not small else 8.4), gy + 4, 3.4 if not small else 2.9, qc, owned and i <= q)
    if foot:   # 底部一行小吊牌（衣柜：所属套装）
        fw = tw(foot, 9, 'b') + 16
        fill_round(p, ox + 9, oy + h - 30, fw, 18, 4, (255, 255, 255, 18)); stroke_round(p, ox + 9, oy + h - 30, fw, 18, 4, (255, 255, 255, 40), .8)
        text(p, ox + 17, oy + h - 21, foot, 9, C(hx('B9BEC0') if owned else hx('7B7F86')), 'b', 'lm')
        barcode(p, ox + w - sw - 46, oy + h - 28, 30, 13, hx('5A5E65') if owned else hx('3E4146'), seed=len(foot))
    # 战力 + 等级
    fy = oy + h - (19 if not small else 13)
    if power and not small:
        spaced(p, ox + 9, fy - 15, 'POWER', 6, C(hx('8C9096')), 'n', .4)
        otext(p, ox + 8, fy - 7, power, 16, hx('FFFFFF'), sw=1.6, kind='n')
    lvs = str(lv) if lv is not None else ''; lfs = 10.5 if not small else 9; lw = tw(lvs, lfs, 'n') + tw('LV.', 6.5, 'n') + 12
    lx, lyy = ax + 4, ay + ah - 20 if not small else ay + ah - 17
    if lv is not None:
        fill_round(p, lx, lyy, lw, 16 if not small else 14, 8, (8, 9, 10, 210))
        stroke_round(p, lx, lyy, lw, 16 if not small else 14, 8, (255, 255, 255, 45), .8)
        lc = lyy + (8 if not small else 7)
        text(p, lx + 5.5, lc + .5, 'LV.', 6.5, C(hx('A6AAB0')), 'n', 'lm'); text(p, lx + lw - 5.5, lc, lvs, lfs, C(hx('FFFFFF')), 'n', 'rm')
    if equipped and not small:   # 左上角斜挂的「穿戴中」缎带
        rib = canvas(w, h); dr = ImageDraw.Draw(rib)
        dr.polygon([(P(3.8), P(3.8 + 22)), (P(3.8 + 22), P(3.8)), (P(3.8 + 40), P(3.8)), (P(3.8), P(3.8 + 40))], fill=C(OR))
        with over(rib) as dl: dl.line([P(3.8), P(3.8 + 23.5), P(3.8 + 23.5), P(3.8)], fill=(255, 255, 255, 180), width=P(1))
        tl = canvas(44, 14); text(tl, 22, 7, '穿戴中', 8.5, C(hx('FFFFFF')), 'k', 'mm'); tl = tl.rotate(45, resample=Image.BICUBIC, expand=True)
        rib.alpha_composite(tl, (P(3.8 + 15.5) - tl.size[0] // 2, P(3.8 + 15.5) - tl.size[1] // 2))
        rib.putalpha(ImageChops.multiply(rib.split()[3], mask_round(rib.size, P(7), P(3.8))))
        p.alpha_composite(rib, (P(ox), P(oy)))
    if selected:
        stroke_round(p, ox - 3, oy - 3, w + 6, h + 6, 11, OR, 2.8)
        ImageDraw.Draw(p).polygon([(P(ox + w / 2 - 7), P(oy + h + 6)), (P(ox + w / 2 + 7), P(oy + h + 6)), (P(ox + w / 2), P(oy + h + 13))], fill=C(OR))
    put(img, p, x - ox, y - oy, angle)
    # 夹子 + 贴纸（不跟着卡旋转，单独贴）
    clip(img, x + w / 2, y + 2)
    if current: tag_sticker(img, x + w - 64, y + h - 34, 58, 19, OR, '当前外观', angle=-5, size=9.5, tc=hx('FFFFFF'))
    if new: sticker_round(img, x + w - 4, y + 2, 11, OR, 'NEW', angle=-14, size=8)
    if locked: sticker_round(img, x + 8, y + h * .55, 8, hx('3B3D42'), '锁', angle=0, size=8, kind='k')
