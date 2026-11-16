"""Layered Q-doll rigid-part prototype.
Parts are single-pose sprites with pivots; motion = keyframed transforms.
Angle convention: arm/leg/weapon angle a: direction (0,1)->(sin a, cos a); + = swing forward (facing +x).
"""
import math, sys
import numpy as np
from PIL import Image, ImageDraw

R = 3                      # render scale (1x -> R px)
CELL_W, CELL_H = 315, 300  # 1x cell size
ROOT_X, GROUND = 96, 222   # 1x
INK = (18, 20, 22, 255)
TILE = (40, 44, 48, 255)
ORANGE = (255, 74, 18, 255)
RED = (255, 77, 94, 255)
PAPER = (246, 246, 242, 255)
STEEL = (150, 163, 176, 255)
STEEL2 = (112, 124, 136, 255)
BG = (232, 233, 228, 255)
SKIN = (205, 210, 214, 255)
GREEN = (107, 212, 148, 255)
BLUE = (89, 166, 255, 255)
GOLD = (255, 194, 77, 255)

# ---------- matrices ----------
def T(x, y): return np.array([[1, 0, x], [0, 1, y], [0, 0, 1.]])
def Rm(a):
    a = math.radians(a); c, s = math.cos(a), math.sin(a)
    return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1.]])
def S(sx, sy): return np.array([[sx, 0, 0], [0, sy, 0], [0, 0, 1.]])

# ---------- part drawing ----------
class Part:
    def __init__(self, w, h, pivot, tip=None):
        self.img = Image.new("RGBA", (int(w * R), int(h * R)), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)
        self.pivot = (pivot[0] * R, pivot[1] * R)
        self.tip = None if tip is None else ((tip[0] - pivot[0]) * R, (tip[1] - pivot[1]) * R)
    def rr(self, x0, y0, x1, y1, r, fill, ow=2):
        self.d.rounded_rectangle([x0 * R, y0 * R, x1 * R, y1 * R], radius=r * R, fill=fill,
                                 outline=INK if ow else None, width=int(ow * R))
    def ell(self, x0, y0, x1, y1, fill, ow=2):
        self.d.ellipse([x0 * R, y0 * R, x1 * R, y1 * R], fill=fill, outline=INK if ow else None, width=int(ow * R))
    def poly(self, pts, fill, ow=2):
        self.d.polygon([(x * R, y * R) for x, y in pts], fill=fill, outline=INK if ow else None)
        if ow:
            self.d.line([(x * R, y * R) for x, y in pts + [pts[0]]], fill=INK, width=int(ow * R), joint="curve")
    def line(self, pts, fill, w=2):
        self.d.line([(x * R, y * R) for x, y in pts], fill=fill, width=int(w * R))

def legs(col, boot):
    p = Part(28, 34, (10, 4))
    p.rr(4, 0, 17, 25, 5, col)
    p.rr(3, 22, 26, 32, 5, boot)
    p.rr(4, 17, 17, 20, 1, ORANGE, ow=0)
    return p

def hand():
    p = Part(16, 16, (8, 8)); p.ell(1, 1, 15, 15, SKIN); return p

# --- armor sets: torso, arms, legs
def armor_light():
    t = Part(54, 48, (27, 46))
    t.rr(4, 2, 50, 46, 13, (70, 84, 96, 255))
    t.rr(17, 14, 41, 31, 5, TILE); t.ell(26, 19, 32, 25, ORANGE, ow=0)
    t.rr(4, 36, 50, 42, 2, ORANGE, ow=0)
    a = Part(20, 46, (10, 7)); a.rr(3, 2, 17, 41, 6, (112, 128, 144, 255)); a.rr(3, 33, 17, 38, 1, ORANGE, ow=0)
    return dict(torso=t, arm=a, armF=a, legs=legs((54, 60, 66, 255), TILE), shoulder=(11, -33), shoulderF=(-4, -33))

def armor_heavy():
    t = Part(64, 54, (32, 52))
    t.rr(2, 2, 62, 52, 10, STEEL)
    t.poly([(14, 10), (50, 10), (44, 38), (20, 38)], STEEL2)
    t.rr(20, 16, 44, 28, 3, TILE); t.rr(24, 20, 40, 24, 1, ORANGE, ow=0)
    t.rr(2, 42, 62, 50, 2, ORANGE, ow=0)
    a = Part(30, 52, (15, 11))
    a.rr(5, 8, 25, 48, 7, (96, 108, 120, 255))
    a.rr(1, 1, 29, 18, 8, STEEL)       # shoulder pad
    a.rr(1, 13, 29, 17, 1, ORANGE, ow=0)
    return dict(torso=t, arm=a, armF=a, legs=legs(STEEL2, TILE), shoulder=(13, -36), shoulderF=(-6, -36))

# --- heads
def head_cap():
    h = Part(88, 78, (44, 70))
    h.rr(8, 14, 78, 68, 24, PAPER)
    h.rr(36, 30, 78, 54, 9, TILE)
    h.rr(50, 36, 57, 48, 2, ORANGE, ow=0); h.rr(63, 36, 70, 48, 2, ORANGE, ow=0)
    h.rr(6, 2, 74, 30, 16, TILE)            # cap dome
    h.rr(42, 24, 86, 31, 3, ORANGE)         # brim
    return h

def head_helm():
    h = Part(92, 82, (46, 74))
    h.rr(4, 10, 82, 72, 26, STEEL)
    h.rr(34, 28, 84, 52, 7, TILE)
    h.rr(44, 37, 80, 43, 2, ORANGE, ow=0)
    h.rr(22, 2, 56, 14, 5, RED)             # crest
    h.rr(4, 58, 82, 66, 2, STEEL2, ow=0)
    return h

def head_visor():
    h = Part(88, 78, (44, 70))
    h.rr(8, 14, 78, 68, 28, (255, 194, 77, 255))
    h.rr(30, 28, 80, 56, 12, TILE)
    h.ell(52, 36, 62, 48, GREEN, ow=0); h.ell(66, 36, 76, 48, GREEN, ow=0)
    h.poly([(14, 22), (4, 6), (26, 14)], (255, 194, 77, 255))
    return h

# --- packs
def pack_small():
    p = Part(28, 52, (24, 40))
    p.rr(0, 16, 26, 50, 7, TILE); p.rr(3, 22, 10, 44, 2, ORANGE, ow=0)
    p.line([(13, 16), (13, 2)], INK, 2); p.ell(9, 0, 17, 8, ORANGE)
    return p

def pack_tank():
    p = Part(40, 64, (34, 44))
    p.rr(0, 8, 32, 60, 12, STEEL2); p.rr(0, 30, 32, 38, 1, ORANGE, ow=0)
    p.rr(10, 0, 22, 12, 3, TILE)
    return p

# --- weapons (drawn along +y from grip pivot)
def wpn_dagger():
    w = Part(20, 74, (10, 6), tip=(10, 72))
    w.rr(6, 0, 14, 12, 2, TILE)
    w.rr(1, 10, 19, 16, 1, ORANGE)
    w.poly([(3, 16), (17, 16), (10, 72)], PAPER)
    return w

def wpn_spear():
    w = Part(18, 128, (9, 38), tip=(9, 126))
    w.rr(7, 0, 11, 112, 1, TILE, ow=1.5)
    w.rr(5, 104, 13, 112, 1, ORANGE)
    w.poly([(3, 110), (15, 110), (9, 126)], PAPER)
    w.rr(5, 34, 13, 44, 2, STEEL2)
    return w

def wpn_hammer():
    w = Part(34, 80, (17, 8), tip=(17, 70))
    w.rr(14, 0, 20, 62, 1, TILE, ow=1.5)
    w.rr(2, 48, 32, 76, 6, STEEL); w.rr(2, 56, 32, 62, 1, ORANGE, ow=0)
    return w

SETS = {
    "A 轻装·短刃": dict(armor=armor_light, head=head_cap, pack=pack_small, wpn=wpn_dagger, kind="dagger"),
    "B 重装·长枪": dict(armor=armor_heavy, head=head_helm, pack=pack_tank, wpn=wpn_spear, kind="spear"),
    "C 混搭·轻甲+重盔+巨锤": dict(armor=armor_light, head=head_helm, pack=pack_tank, wpn=wpn_hammer, kind="hammer"),
}


# ---------- load a delivered art set from manifest (parts.json) ----------
import json, os
def part_from_png(path, pivot, scale, tip=None):
    """scale = pixels per 1x unit in the delivered PNG (recommended 4)."""
    im = Image.open(path).convert("RGBA")
    k = R / scale
    im = im.resize((max(1, round(im.size[0] * k)), max(1, round(im.size[1] * k))), Image.LANCZOS)
    p = Part.__new__(Part)
    p.img = im; p.d = None
    p.pivot = (pivot[0] * R, pivot[1] * R)
    p.tip = None if tip is None else ((tip[0] - pivot[0]) * R, (tip[1] - pivot[1]) * R)
    return p

def set_from_manifest(path, defaults=None):
    """Manifest format: see Docs/给素材会话的任务清单_v0.12_分层部件.md."""
    base = os.path.dirname(path)
    m = json.load(open(path, encoding="utf-8"))
    sc = m.get("scale", 4)
    def L(key):
        e = m["parts"][key]
        return part_from_png(os.path.join(base, e["file"]), e["pivot"], sc, e.get("tip"))
    def armor():
        return dict(torso=L("torso"), arm=L("arm"), armF=L("armF") if "armF" in m["parts"] else L("arm"),
                    legs=L("legs"), shoulder=tuple(m.get("shoulder", (11, -33))),
                    shoulderF=tuple(m.get("shoulderF", (-4, -33))))
    return dict(armor=armor, head=lambda: L("head"), pack=lambda: L("pack"), wpn=lambda: L("weapon"),
                kind=m.get("weapon_class", "dagger"))

# ---------- animation ----------
def sm(x): x = max(0., min(1., x)); return x * x * (3 - 2 * x)
def kf(t, keys):
    if t <= keys[0][0]: return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1: return v0 + (v1 - v0) * sm((t - t0) / (t1 - t0))
    return keys[-1][1]

def pose(kind, anim, t):
    ph = 2 * math.pi * t
    p = dict(dx=0, dy=0, sx=1, sy=1, lean=4, head=0, pack=0, A=8, Af=-8, L=0, Lf=0, W=None, wt=0, fx=False)
    if anim == "idle":
        p['dy'] = -4 * (0.5 - 0.5 * math.cos(ph)); sq = math.cos(ph)
        p['sy'] = 1 - 0.035 * sq; p['sx'] = 1 + 0.03 * sq
        p['lean'] = 3 + 1.5 * math.sin(ph - .5)
        p['head'] = -2.5 * math.sin(ph - 1.0); p['pack'] = -5 * math.sin(ph - 1.6)
        p['A'] = 8 + 6 * math.sin(ph - .7); p['Af'] = -8 - 6 * math.sin(ph - .7)
    elif anim == "walk":
        p['dy'] = -9 * abs(math.sin(ph)); sq = math.cos(2 * ph)
        p['sy'] = 1 - 0.06 * sq; p['sx'] = 1 + 0.05 * sq
        p['lean'] = 7 + 2 * math.sin(2 * ph); p['head'] = -4 * math.sin(2 * ph - 1.0)
        p['pack'] = -9 * math.sin(2 * ph - 1.6)
        p['L'] = 34 * math.sin(ph); p['Lf'] = -34 * math.sin(ph)
        p['A'] = -32 * math.sin(ph) + 6; p['Af'] = 32 * math.sin(ph) - 6
    elif anim == "attack":
        t2 = t
        if kind == "dagger":
            p['dx'] = kf(t2, [(0, 0), (.30, -10), (.46, 30), (.62, 30), (1, 0)])
            p['lean'] = kf(t2, [(0, 4), (.30, -12), (.46, 20), (.62, 16), (1, 4)])
            p['A'] = kf(t2, [(0, 8), (.30, -75), (.44, 92), (.62, 80), (1, 8)])
            p['W'] = kf(t2, [(0, 110), (.30, 215), (.44, 38), (.62, 50), (1, 110)])
            p['Af'] = kf(t2, [(0, -8), (.30, 40), (.44, -50), (.62, -40), (1, -8)])
            p['head'] = kf(t2, [(0, 0), (.30, 6), (.46, -8), (1, 0)])
            p['pack'] = kf(t2, [(0, 0), (.30, 10), (.5, -16), (.7, 6), (1, 0)])
            p['L'] = kf(t2, [(0, 0), (.30, -18), (.46, 38), (.62, 38), (1, 0)])
            p['Lf'] = kf(t2, [(0, 0), (.30, 14), (.46, -26), (.62, -26), (1, 0)])
            p['dy'] = kf(t2, [(0, 0), (.30, 2), (.40, -14), (.50, 0), (1, 0)])
            p['sy'] = kf(t2, [(0, 1), (.30, .93), (.40, 1.06), (.50, .94), (.7, 1), (1, 1)])
            p['sx'] = 2 - p['sy'] if False else 1 + (1 - p['sy']) * .8
            p['fx'] = .32 < t2 < .72
        elif kind == "spear":
            p['dx'] = kf(t2, [(0, 0), (.32, -12), (.46, 30), (.64, 30), (1, 0)])
            p['lean'] = kf(t2, [(0, 4), (.32, -10), (.46, 14), (.64, 12), (1, 4)])
            p['A'] = kf(t2, [(0, 8), (.32, -35), (.46, 62), (.64, 55), (1, 8)])
            p['W'] = kf(t2, [(0, 170), (.32, 100), (.46, 86), (.64, 90), (1, 170)])
            p['wt'] = kf(t2, [(0, 0), (.32, -26), (.46, 34), (.64, 26), (1, 0)])
            p['Af'] = kf(t2, [(0, -8), (.32, -45), (.46, 40), (.64, 30), (1, -8)])
            p['head'] = kf(t2, [(0, 0), (.32, 5), (.46, -7), (1, 0)])
            p['pack'] = kf(t2, [(0, 0), (.32, 8), (.5, -14), (.7, 5), (1, 0)])
            p['L'] = kf(t2, [(0, 0), (.32, -14), (.46, 30), (.64, 30), (1, 0)])
            p['Lf'] = kf(t2, [(0, 0), (.32, 10), (.46, -22), (.64, -22), (1, 0)])
            p['sy'] = kf(t2, [(0, 1), (.32, .94), (.46, 1.04), (.54, .95), (.75, 1), (1, 1)])
            p['sx'] = 1 + (1 - p['sy']) * .8
            p['fx'] = .36 < t2 < .72
        else:  # hammer: big overhead smash
            p['dx'] = kf(t2, [(0, 0), (.38, -8), (.52, 22), (.7, 22), (1, 0)])
            p['dy'] = kf(t2, [(0, 0), (.30, -6), (.38, -16), (.50, 0), (1, 0)])
            p['lean'] = kf(t2, [(0, 4), (.38, -16), (.52, 24), (.7, 20), (1, 4)])
            p['A'] = kf(t2, [(0, 8), (.38, -110), (.52, 78), (.7, 70), (1, 8)])
            p['W'] = kf(t2, [(0, 120), (.38, 235), (.52, 20), (.7, 30), (1, 120)])
            p['Af'] = kf(t2, [(0, -8), (.38, -100), (.52, 60), (.7, 50), (1, -8)])
            p['head'] = kf(t2, [(0, 0), (.38, 8), (.52, -10), (1, 0)])
            p['pack'] = kf(t2, [(0, 0), (.38, 12), (.56, -18), (.8, 6), (1, 0)])
            p['L'] = kf(t2, [(0, 0), (.38, -10), (.52, 28), (.7, 28), (1, 0)])
            p['Lf'] = kf(t2, [(0, 0), (.38, 12), (.52, -20), (.7, -20), (1, 0)])
            p['sy'] = kf(t2, [(0, 1), (.30, .92), (.38, 1.07), (.52, .90), (.62, .96), (.8, 1), (1, 1)])
            p['sx'] = 1 + (1 - p['sy']) * .8
            p['fx'] = .40 < t2 < .78
    if p['W'] is None:  # weapon rides along with the arm
        base = {"dagger": 95, "spear": 168, "hammer": 112}[kind]
        p['W'] = p['A'] * (1 if kind != "spear" else .35) + base
    return p

def matrices(parts, p):
    root = T((ROOT_X + p['dx']) * R, (GROUND + p['dy']) * R) @ S(p['sx'], p['sy'])
    torso = root @ T(0, -24 * R) @ Rm(-p['lean'])
    head = torso @ T(2 * R, -38 * R) @ Rm(-p['head'])
    pack = torso @ T(-22 * R, -26 * R) @ Rm(-p['pack'])
    sh, shf = parts['armor']['shoulder'], parts['armor']['shoulderF']
    armN = torso @ T(sh[0] * R, sh[1] * R) @ Rm(p['A'])
    armF = torso @ T(shf[0] * R, shf[1] * R) @ Rm(p['Af'])
    ap = parts['armor']['arm']
    handM = armN @ T(0, (ap.img.size[1] / R - 11) * R - ap.pivot[1])  # hand point = 11 units above image bottom
    wM = handM @ Rm(p['W'] - p['A']) @ T(0, p['wt'] * R)
    legN = root @ T(5 * R, -24 * R) @ Rm(p['L'])
    legF = root @ T(-5 * R, -24 * R) @ Rm(p['Lf'])
    return dict(root=root, torso=torso, head=head, pack=pack, armN=armN, armF=armF, hand=handM, wpn=wM,
                legN=legN, legF=legF)

def blit(canvas, part, M):
    full = M @ T(-part.pivot[0], -part.pivot[1])
    inv = np.linalg.inv(full)
    coeff = (inv[0, 0], inv[0, 1], inv[0, 2], inv[1, 0], inv[1, 1], inv[1, 2])
    tr = part.img.transform(canvas.size, Image.AFFINE, coeff, resample=Image.BICUBIC)
    canvas.alpha_composite(tr)

def tip_of(parts, kind, anim, t):
    p = pose(kind, anim, t); m = matrices(parts, p)
    tp = parts['wpn'].tip
    v = m['wpn'] @ np.array([tp[0], tp[1], 1.])
    return v[0], v[1]

def render(sp, anim, t):
    parts = dict(armor=sp['armor'](), head=sp['head'](), pack=sp['pack'](), wpn=sp['wpn'](), hand=hand())
    kind = sp['kind']
    p = pose(kind, anim, t); m = matrices(parts, p)
    cv = Image.new("RGBA", (300 * R, 260 * R), BG)
    d = ImageDraw.Draw(cv, "RGBA")
    d.line([(0, GROUND * R), (300 * R, GROUND * R)], fill=(18, 20, 22, 60), width=2 * R)
    h = -p['dy']
    sw = (54 - h * 0.8) * R; sh_ = (9 - h * 0.12) * R
    cx = (ROOT_X + p['dx']) * R
    d.ellipse([cx - sw / 2, GROUND * R - sh_ / 2, cx + sw / 2, GROUND * R + sh_ / 2], fill=(18, 20, 22, 70))
    a = parts['armor']
    blit(cv, parts['pack'], m['pack'])
    blit(cv, a['armF'], m['armF'])
    blit(cv, a['legs'], m['legF'])
    blit(cv, a['legs'], m['legN'])
    blit(cv, a['torso'], m['torso'])
    blit(cv, parts['head'], m['head'])
    blit(cv, a['arm'], m['armN'])
    blit(cv, parts['wpn'], m['wpn'])
    blit(cv, parts['hand'], m['hand'])
    # tip trail
    if p['fx']:
        pts = []
        for k in range(17):
            tt = t - k * 0.009
            pts.append(tip_of(parts, kind, anim, max(0, tt)))
        for i in range(len(pts) - 1):
            al = int(230 * (1 - i / 16)); wd = int((9 - i * .5) * R * (0.9 if kind != "spear" else 0.55))
            d.line([pts[i], pts[i + 1]], fill=(255, 120, 40, al), width=max(2, wd))
            d.line([pts[i], pts[i + 1]], fill=(255, 255, 255, int(al * .8)), width=max(1, wd // 3))
    cv = cv.crop((20 * R, 50 * R, 230 * R, 250 * R))
    return cv.resize((CELL_W, CELL_H), Image.LANCZOS)

def sheet(anim, nframes, sets, path, dur=70):
    frames = []
    for i in range(nframes):
        t = i / nframes
        sh = Image.new("RGBA", (CELL_W * len(sets), CELL_H + 22), BG)
        for j, (name, sp) in enumerate(sets.items()):
            sh.alpha_composite(render(sp, anim, t), (j * CELL_W, 22))
        frames.append(sh.convert("RGB"))
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=dur, loop=0, optimize=False)
    return frames

if __name__ == "__main__":
    # python rig.py [outdir] [manifest1.json manifest2.json ...]
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    sets = SETS if len(sys.argv) <= 2 else {os.path.basename(os.path.dirname(os.path.abspath(a))): set_from_manifest(a) for a in sys.argv[2:]}
    for anim, n in (("idle", 14), ("walk", 14), ("attack", 20)):
        fr = sheet(anim, n, sets, f"{out}/rig_{anim}.gif", dur=70 if anim != "idle" else 90)
        fr[n // 2].save(f"{out}/rig_{anim}_mid.png")
        print(anim, "ok", len(fr))


def export_selftest(outdir):
    """Export the built-in placeholder set as PNG+parts.json (also a reference sample for the art side)."""
    os.makedirs(outdir, exist_ok=True)
    for name, sp in SETS.items():
        tag = name.split()[0]
        d = os.path.join(outdir, tag); os.makedirs(d, exist_ok=True)
        ar = sp['armor'](); parts = {}
        def save(key, part, scale_to=4):
            im = part.img.resize((round(part.img.size[0] * scale_to / R), round(part.img.size[1] * scale_to / R)), Image.LANCZOS)
            pad = 2 * scale_to  # 2 units transparent margin on every side
            big = Image.new("RGBA", (im.size[0] + 2 * pad, im.size[1] + 2 * pad), (0, 0, 0, 0)); big.paste(im, (pad, pad))
            big.save(os.path.join(d, key + ".png"))
            ent = dict(file=key + ".png", pivot=[part.pivot[0] / R + 2, part.pivot[1] / R + 2])
            if part.tip: ent["tip"] = [part.tip[0] / R + part.pivot[0] / R + 2, part.tip[1] / R + part.pivot[1] / R + 2]
            parts[key] = ent
        save("torso", ar['torso']); save("arm", ar['arm']); save("legs", ar['legs'])
        save("head", sp['head']()); save("pack", sp['pack']()); save("weapon", sp['wpn']())
        json.dump(dict(scale=4, weapon_class=sp['kind'], shoulder=list(ar['shoulder']), shoulderF=list(ar['shoulderF']),
                       parts=parts), open(os.path.join(d, "parts.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
