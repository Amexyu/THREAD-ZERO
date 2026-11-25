"""例图工具：按游戏的逻辑坐标（1600×900）画，SS 倍超采样，最后缩到 1920×1080。"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import math, random

SS = 2.4                         # 逻辑单位 → 超采样像素（1.2 × 2）
W, H = int(1600 * SS), int(900 * SS)
CJK = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
CJKB = '/usr/share/fonts/opentype/noto/NotoSansCJK-Black.ttc'
CJKR = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
OXA = '/home/claude/icon/Oxanium-Bold.ttf'
_fc = {}
def font(size, kind='b'):
    key = (size, kind)
    if key not in _fc:
        path = {'b': CJK, 'k': CJKB, 'r': CJKR, 'n': OXA}[kind]
        _fc[key] = ImageFont.truetype(path, max(1, int(size * SS)), index=2) if kind != 'n' else ImageFont.truetype(path, max(1, int(size * SS)))
    return _fc[key]

def hx(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)
INK = hx('111213'); BG = hx('3C3D3E'); PANEL = hx('48494A'); TILE = hx('1A1B1C'); TILE2 = hx('262728')
WHITE = hx('F4F4F1'); MUTE = hx('AEB0AF'); GREY = hx('8E8F8D'); ORANGE = hx('FF4A12'); TEAL = hx('45E6D6')
Q = [hx('9CABB8'), hx('6BD494'), hx('59A6FF'), hx('BF80FF'), hx('FFC24D'), hx('FF4D5E'), hx('45E6D6')]
QN = ['量产', '改良', '特装', '军规', '机密', '原型', '零号']

def P(v): return int(round(v * SS))
def box(x, y, w, h): return [P(x), P(y), P(x + w), P(y + h)]

def new(bg=BG): return Image.new('RGBA', (W, H), bg)

def rect(img, x, y, w, h, fill, r=0, outline=None, width=0):
    d = ImageDraw.Draw(img)
    if r > 0: d.rounded_rectangle(box(x, y, w, h), radius=P(r), fill=fill, outline=outline, width=P(width) if width else 0)
    else: d.rectangle(box(x, y, w, h), fill=fill, outline=outline, width=P(width) if width else 0)

def layer(): return Image.new('RGBA', (W, H), (0, 0, 0, 0))

def vgrad(img, x, y, w, h, top, bottom, r=0):
    g = Image.new('RGBA', (1, 256))
    for i in range(256):
        u = i / 255; g.putpixel((0, i), tuple(int(top[c] + (bottom[c] - top[c]) * u) for c in range(4)))
    g = g.resize((max(1, P(w)), max(1, P(h))), Image.BILINEAR)
    m = Image.new('L', g.size, 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, g.size[0] - 1, g.size[1] - 1], radius=P(r), fill=255)
    img.paste(g, (P(x), P(y)), ImageChops.multiply(m, g.split()[3]))

def hgrad(img, x, y, w, h, left, right):
    g = Image.new('RGBA', (256, 1))
    for i in range(256):
        u = i / 255; g.putpixel((i, 0), tuple(int(left[c] + (right[c] - left[c]) * u) for c in range(4)))
    g = g.resize((max(1, P(w)), max(1, P(h))), Image.BILINEAR)
    img.alpha_composite(g, (P(x), P(y)))

def glow(img, cx, cy, r, color, alpha=1.0, soft=.5):
    l = layer(); m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).ellipse([P(cx - r), P(cy - r), P(cx + r), P(cy + r)], fill=int(255 * alpha))
    m = m.filter(ImageFilter.GaussianBlur(P(r * soft)))
    l = Image.new('RGBA', (W, H), color[:3] + (0,)); l.putalpha(m); img.alpha_composite(l)

def text(img, x, y, s, size, fill=WHITE, kind='b', anchor='la', stroke=0, stroke_fill=INK, spacing=0):
    d = ImageDraw.Draw(img)
    d.text((P(x), P(y)), s, font=font(size, kind), fill=fill, anchor=anchor, stroke_width=P(stroke) if stroke else 0, stroke_fill=stroke_fill)

def tw(s, size, kind='b'):
    return font(size, kind).getlength(s) / SS

def paste(img, src, x, y, w=None, h=None, alpha=1.0):
    if w is not None: src = src.resize((P(w), P(h) if h else int(src.size[1] * P(w) / src.size[0])), Image.LANCZOS)
    if alpha < 1: a = src.split()[3].point(lambda v: int(v * alpha)); src = src.copy(); src.putalpha(a)
    img.alpha_composite(src, (P(x), P(y)))

def shadow(img, x, y, w, h, r=8, blur=8, alpha=.5, dy=6):
    m = Image.new('L', (W, H), 0)
    ImageDraw.Draw(m).rounded_rectangle(box(x, y + dy, w, h), radius=P(r), fill=int(255 * alpha))
    m = m.filter(ImageFilter.GaussianBlur(P(blur)))
    l = Image.new('RGBA', (W, H), (0, 0, 0, 0)); l.putalpha(m); img.alpha_composite(l)

def diamond(img, cx, cy, r, fill):
    ImageDraw.Draw(img).polygon([(P(cx), P(cy - r)), (P(cx + r), P(cy)), (P(cx), P(cy + r)), (P(cx - r), P(cy))], fill=fill)

def finish(img, out, nav=None):
    final = img.resize((1920, 1080), Image.LANCZOS)
    if nav is not None: final.alpha_composite(nav, (0, 0))
    final.convert('RGB').save(out)
    return final
