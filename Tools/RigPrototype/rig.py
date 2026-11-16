"""Layered Q-doll rigid-part prototype.
Parts are single-pose sprites with pivots; motion = keyframed transforms.
Angle convention: arm/leg/weapon angle a: direction (0,1)->(sin a, cos a); + = swing forward (facing +x).
"""
import math, sys
import numpy as np
from PIL import Image, ImageOps, ImageDraw

R = 3                      # render scale (1x -> R px)
CELL_W, CELL_H = 360, 300  # 1x cell size
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
    p = Part(32, 26, (11, 4))
    p.rr(4, 0, 19, 18, 6, col)
    p.rr(3, 14, 29, 25, 6, boot)
    p.rr(4, 10, 19, 13, 1, ORANGE, ow=0)
    return p

def hand():
    p = Part(18, 18, (9, 9)); p.ell(1, 1, 17, 17, SKIN); return p

# --- armor sets: torso, arms, legs   (Q proportions v0.13: big head, block torso, stubby limbs)
def armor_light():
    t = Part(52, 38, (26, 36))
    t.rr(3, 2, 49, 36, 11, (70, 84, 96, 255))
    t.rr(15, 9, 37, 23, 4, TILE); t.ell(23, 13, 29, 19, ORANGE, ow=0)
    t.rr(3, 28, 49, 33, 2, ORANGE, ow=0)
    a = Part(20, 32, (10, 6)); a.rr(3, 2, 17, 29, 6, (112, 128, 144, 255)); a.rr(3, 22, 17, 26, 1, ORANGE, ow=0)
    return dict(torso=t, arm=a, armF=a, legN=legs((54, 60, 66, 255), TILE), legF=legs((54, 60, 66, 255), TILE), shoulder=(11, -26), shoulderF=(-4, -26))

def armor_heavy():
    t = Part(60, 42, (30, 40))
    t.rr(2, 2, 58, 40, 10, STEEL)
    t.poly([(12, 8), (48, 8), (43, 30), (17, 30)], STEEL2)
    t.rr(18, 12, 42, 22, 3, TILE); t.rr(22, 15, 38, 19, 1, ORANGE, ow=0)
    t.rr(2, 32, 58, 38, 2, ORANGE, ow=0)
    a = Part(30, 36, (15, 10))
    a.rr(5, 7, 25, 33, 7, (96, 108, 120, 255))
    a.rr(1, 1, 29, 15, 8, STEEL)       # shoulder pad
    a.rr(1, 11, 29, 14, 1, ORANGE, ow=0)
    return dict(torso=t, arm=a, armF=a, legN=legs(STEEL2, TILE), legF=legs(STEEL2, TILE), shoulder=(13, -29), shoulderF=(-6, -29))

# --- heads (big: ~half of total height)
def head_cap():
    h = Part(96, 84, (48, 76))
    h.rr(6, 12, 90, 76, 30, PAPER)
    h.rr(40, 32, 90, 58, 10, TILE)
    h.rr(55, 38, 63, 52, 2, ORANGE, ow=0); h.rr(70, 38, 78, 52, 2, ORANGE, ow=0)
    h.rr(4, 2, 84, 32, 18, TILE)            # cap dome
    h.rr(48, 26, 94, 33, 3, ORANGE)         # brim
    return h

def head_helm():
    h = Part(100, 88, (50, 80))
    h.rr(4, 10, 94, 80, 32, STEEL)
    h.rr(38, 32, 96, 58, 8, TILE)
    h.rr(50, 42, 92, 48, 2, ORANGE, ow=0)
    h.rr(26, 2, 64, 14, 5, RED)             # crest
    h.rr(4, 64, 94, 72, 2, STEEL2, ow=0)
    return h

def head_visor():
    h = Part(96, 84, (48, 76))
    h.rr(6, 12, 90, 76, 32, (255, 194, 77, 255))
    h.rr(32, 30, 90, 62, 14, TILE)
    h.ell(56, 40, 68, 54, GREEN, ow=0); h.ell(72, 40, 84, 54, GREEN, ow=0)
    h.poly([(14, 22), (4, 6), (28, 14)], (255, 194, 77, 255))
    return h

# --- packs
def pack_small():
    p = Part(26, 42, (23, 30))
    p.rr(0, 12, 24, 40, 7, TILE); p.rr(3, 18, 9, 36, 2, ORANGE, ow=0)
    p.line([(12, 12), (12, 2)], INK, 2); p.ell(8, 0, 16, 8, ORANGE)
    return p

def pack_tank():
    p = Part(36, 54, (31, 34))
    p.rr(0, 8, 32, 52, 12, STEEL2); p.rr(0, 28, 32, 35, 1, ORANGE, ow=0)
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
    w = Part(18, 122, (9, 36), tip=(9, 120))
    w.rr(7, 0, 11, 108, 1, TILE, ow=1.5)
    w.rr(5, 100, 13, 108, 1, ORANGE)
    w.poly([(3, 106), (15, 106), (9, 120)], PAPER)
    w.rr(5, 32, 13, 42, 2, STEEL2)
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
    P = m["parts"]
    def armor():
        sh_a = tuple(m.get("shoulder", (11, -26))); sh_b = tuple(m.get("shoulderF", (-4, -26)))
        # manifest 'shoulder' = weapon (near) arm, drawn in front; 'shoulderF' = far arm, drawn behind the torso
        d = dict(torso=L("torso"), arm=L("arm"), armF=L("armF") if "armF" in P else L("arm"),
                 shoulder=sh_a, shoulderF=sh_b)
        if "leg_near" in P:
            d["legN"] = L("leg_near"); d["legF"] = L("leg_far")
        else:
            d["legN"] = d["legF"] = L("legs")
        if "hand" in P["arm"]:
            hx, hy = P["arm"]["hand"]; px, py = P["arm"]["pivot"]
            d["hand_pt"] = (hx - px, hy - py)
        if "rig" in m: d["rig"] = m["rig"]
        return d
    sp = dict(armor=armor, head=lambda: L("head"), pack=lambda: L("pack"), wpn=lambda: L("weapon"),
              kind=m.get("weapon_class", "dagger"))
    if "hand" in P: sp["hand"] = lambda: L("hand")
    return sp

# ---------- animation ----------
def sm(x): x = max(0., min(1., x)); return x * x * (3 - 2 * x)
def kf(t, keys):
    if t <= keys[0][0]: return keys[0][1]
    for (t0, v0), (t1, v1) in zip(keys, keys[1:]):
        if t <= t1: return v0 + (v1 - v0) * sm((t - t0) / (t1 - t0))
    return keys[-1][1]

def pose(kind, anim, t):
    ph = 2 * math.pi * t
    p = dict(dx=0, dy=0, sx=1, sy=1, tilt=0, lean=1, head=0, pack=0, A=8, Af=-8, L=0, Lf=0, W=None, wt=0, fx=False)
    if anim == "idle":
        p['dy'] = -5 * (0.5 - 0.5 * math.cos(ph)); sq = math.cos(ph)
        p['sy'] = 1 - 0.04 * sq; p['sx'] = 1 + 0.035 * sq; p['tilt'] = 1.6 * math.sin(ph)
        p['lean'] = 0.5 + 1.2 * math.sin(ph - .5)
        p['head'] = -2.5 * math.sin(ph - 1.0); p['pack'] = -2 * math.sin(ph - 1.6)
        p['A'] = 1 + 3 * math.sin(ph - .7); p['Af'] = -1 - 3 * math.sin(ph - .7)
    elif anim == "walk":
        sv = abs(math.sin(ph)); p['dy'] = -9 * sv
        k = sm((sv - .15) / .45)                      # 0 at landing, 1 in the air
        p['sy'] = .965 + .06 * k; p['sx'] = 2 - p['sy']  # landing squash / airborne stretch
        p['tilt'] = 3.2 * math.sin(ph)                 # whole-body rock about the feet
        p['lean'] = 1; p['head'] = -4 * math.sin(ph - 1.0); p['pack'] = -4 * math.sin(ph - 1.6)
        p['L'] = 7 * math.sin(ph) - 3 * sv; p['Lf'] = -7 * math.sin(ph) - 3 * sv
        p['A'] = -6 * math.sin(ph - .6) + 1; p['Af'] = 6 * math.sin(ph - .6) - 1
        p['dust'] = sv < .15
    elif anim == "attack":
        t2 = t
        if kind == "dagger":
            p['dx'] = kf(t2, [(0, 0), (.28, -8), (.42, 26), (.62, 26), (1, 0)])
            p['tilt'] = kf(t2, [(0, 0), (.28, -5), (.42, 8), (.62, 6), (1, 0)])
            p['lean'] = kf(t2, [(0, 1), (.28, -4), (.42, 6), (.62, 4), (1, 1)])
            p['A'] = kf(t2, [(0, 1), (.28, -16), (.42, 92), (.62, 86), (1, 1)])
            p['shx'] = kf(t2, [(0, 0), (.28, -3), (.42, 13), (.62, 11), (1, 0)])   # torso twist: weapon shoulder comes forward
            p['W'] = kf(t2, [(0, 58), (.28, 92), (.42, 95), (.62, 92), (1, 58)])
            p['wt'] = 0   # weapon never slides in the grip
            p['Af'] = kf(t2, [(0, -1), (.28, 10), (.42, -14), (.62, -12), (1, -1)])   # off hand stays behind the body, swings back as counterweight
            p['head'] = kf(t2, [(0, 0), (.28, 5), (.42, -6), (1, 0)])
            p['pack'] = kf(t2, [(0, 0), (.28, 3), (.46, -5), (.7, 2), (1, 0)])
            # front foot steps forward, rear leg pushes back: legs never cross (see matrices: L drives the leg with larger x)
            p['L'] = kf(t2, [(0, 0), (.28, -8), (.42, 24), (.62, 24), (1, 0)])    # L = front leg
            p['Lf'] = kf(t2, [(0, 0), (.28, 6), (.42, -20), (.62, -20), (1, 0)])   # Lf = rear leg
            p['dy'] = kf(t2, [(0, 0), (.28, 2), (.36, -6), (.46, 0), (1, 0)])
            p['sy'] = kf(t2, [(0, 1), (.28, .95), (.36, 1.04), (.46, .95), (.7, 1), (1, 1)])
            p['sx'] = 2 - p['sy']
            p['fx'] = .36 < t2 < .62
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
    elif anim == "combo":   # 迅切连击 = three thrusts, the last one a dash; hits at t = .338/.471/.75 (DaggerSkill.Hits / .68s)
        p['dx'] = kf(t, [(0, 0), (.16, -8), (.26, -4), (.338, 20), (.40, 14), (.471, 32), (.60, 18), (.75, 66), (.86, 66), (1, 0)])
        p['A'] = kf(t, [(0, 1), (.16, -18), (.26, -10), (.338, 92), (.40, 30), (.471, 94), (.60, -22), (.75, 96), (.86, 92), (1, 1)])
        p['W'] = kf(t, [(0, 58), (.16, 88), (.26, 86), (.338, 94), (.40, 80), (.471, 94), (.60, 90), (.75, 95), (.86, 92), (1, 58)])
        p['shx'] = kf(t, [(0, 0), (.16, -4), (.26, -3), (.338, 12), (.40, 6), (.471, 13), (.60, -4), (.75, 15), (.86, 13), (1, 0)])
        p['tilt'] = kf(t, [(0, 0), (.16, -6), (.338, 6), (.40, 3), (.471, 7), (.60, -7), (.75, 11), (.86, 9), (1, 0)])
        p['lean'] = kf(t, [(0, 1), (.16, -5), (.338, 6), (.40, 3), (.471, 7), (.60, -6), (.75, 10), (.86, 8), (1, 1)])
        p['Af'] = kf(t, [(0, -1), (.16, 8), (.338, -12), (.40, -6), (.471, -14), (.60, 10), (.75, -20), (1, -1)])
        p['head'] = kf(t, [(0, 0), (.16, 5), (.338, -5), (.471, -5), (.60, 5), (.75, -7), (1, 0)])
        p['pack'] = kf(t, [(0, 0), (.16, 3), (.36, -4), (.48, -4), (.60, 3), (.78, -6), (1, 0)])
        p['L'] = kf(t, [(0, 0), (.16, -8), (.338, 20), (.40, 16), (.471, 24), (.60, -8), (.75, 30), (.86, 30), (1, 0)])
        p['Lf'] = kf(t, [(0, 0), (.16, 8), (.338, -18), (.40, -14), (.471, -20), (.60, 8), (.75, -26), (.86, -26), (1, 0)])
        p['dy'] = kf(t, [(0, 0), (.16, 3), (.338, 0), (.60, 3), (.68, -6), (.75, 0), (1, 0)])
        p['sy'] = kf(t, [(0, 1), (.16, .92), (.30, 1.03), (.40, .97), (.47, 1.03), (.60, .91), (.72, 1.06), (.80, .95), (.92, 1), (1, 1)])
        p['sx'] = 2 - p['sy']
        p['fx'] = .29 < t < .36 or .42 < t < .49 or .66 < t < .80
        p['ghost'] = .28 < t < .50 or .64 < t < .84          # orange afterimages while dashing
        p['glint'] = .08 < t < .20 or .54 < t < .62           # blade flash on wind-up
    if p['W'] is None:  # weapon rides along with the arm
        base = {"dagger": 58, "spear": 168, "hammer": 112}[kind]
        p['W'] = p['A'] * (0.3 if kind == "dagger" else (1 if kind != "spear" else .35)) + base
    return p

def matrices(parts, p):
    rg = parts['armor'].get('rig') or {}
    hip = rg.get('hip', (0, -21)); hd = rg.get('head', (2, -29)); pk = rg.get('pack', (-21, -20))
    ln = rg.get('leg_near', (5, 0)); lf = rg.get('leg_far', (-5, 0))
    root = T((ROOT_X + p['dx']) * R, (GROUND + p['dy']) * R) @ Rm(-p.get('tilt', 0)) @ S(p['sx'], p['sy'])
    torso = root @ T(hip[0] * R, hip[1] * R) @ Rm(-p['lean'])
    head = torso @ T(hd[0] * R, hd[1] * R) @ Rm(-p['head']) @ S(1 / p['sx'], 1 / p['sy'])  # head keeps its shape; only the body squashes
    pack = torso @ T(pk[0] * R, pk[1] * R) @ Rm(-p['pack'])
    sh, shf = parts['armor']['shoulder'], parts['armor']['shoulderF']
    armN = torso @ T((sh[0] + p.get('shx', 0)) * R, sh[1] * R) @ Rm(p['A'])
    armF = torso @ T(shf[0] * R, shf[1] * R) @ Rm(p['Af'])
    ap = parts['armor']['arm']
    hp = parts['armor'].get('hand_pt')
    if hp: handM = armN @ T(hp[0] * R, hp[1] * R)   # explicit hand point from manifest (arm.hand)
    else: handM = armN @ T(0, (ap.img.size[1] / R - 11) * R - ap.pivot[1])  # default: 11 units above image bottom
    handF = armF @ T(*((hp[0] * R, hp[1] * R) if hp else (0, (ap.img.size[1] / R - 11) * R - ap.pivot[1])))
    wM = handM @ Rm(p['W'] - p['A']) @ T(0, p['wt'] * R)
    aN, aF = (p['L'], p['Lf']) if ln[0] >= lf[0] else (p['Lf'], p['L'])   # pose L/Lf = front/rear leg
    legN = root @ T((hip[0] + ln[0]) * R, (hip[1] + ln[1]) * R) @ Rm(aN)
    legF = root @ T((hip[0] + lf[0]) * R, (hip[1] + lf[1]) * R) @ Rm(aF)
    return dict(root=root, torso=torso, head=head, pack=pack, armN=armN, armF=armF, hand=handM, handF=handF, wpn=wM,
                legN=legN, legF=legF)

def far_side(part, shade=0.62):
    """Far-side copy of a limb: mirrored (so it is not a clone of the near limb) and darkened (reads as further away)."""
    q = Part.__new__(Part); q.d = None
    im = ImageOps.mirror(part.img)
    r, g, b, a = im.split()
    r, g, b = [c.point(lambda v: int(v * shade)) for c in (r, g, b)]
    q.img = Image.merge("RGBA", (r, g, b, a))
    q.pivot = (part.img.size[0] - part.pivot[0], part.pivot[1])
    q.tip = None if part.tip is None else (-part.tip[0], part.tip[1])
    return q

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

COMBO_HITS = (.338, .471, .75)
ORANGE = (255, 74, 18)

def silhouette(parts, m, color, alpha):
    """Solid-colour copy of the whole body (afterimage)."""
    lay = Image.new("RGBA", (300 * R, 260 * R), (0, 0, 0, 0))
    a = parts['armor']
    for part, key in ((parts['pack'], 'pack'), (a['legF'], 'legF'), (a['legN'], 'legN'), (a['torso'], 'torso'),
                      (parts['head'], 'head'), (a['arm'], 'armN'), (parts['wpn'], 'wpn'), (parts['hand'], 'hand')):
        blit(lay, part, m[key])
    al = lay.split()[3].point(lambda v: int(v * alpha))
    solid = Image.new("RGBA", lay.size, color + (255,)); solid.putalpha(al)
    return solid

def star(d, x, y, r, w, col):
    for ang in (0, 90, 45, 135):
        c, s_ = math.cos(math.radians(ang)), math.sin(math.radians(ang)); rr = r if ang % 90 == 0 else r * .55
        d.line([(x - c * rr, y - s_ * rr), (x + c * rr, y + s_ * rr)], fill=col, width=max(1, int(w if ang % 90 == 0 else w * .6)))

def render(sp, anim, t):
    parts = dict(armor=sp['armor'](), head=sp['head'](), pack=sp['pack'](), wpn=sp['wpn'](), hand=sp['hand']() if 'hand' in sp else hand())
    kind = sp['kind']
    p = pose(kind, anim, t); m = matrices(parts, p)
    cv = Image.new("RGBA", (300 * R, 260 * R), BG)
    d = ImageDraw.Draw(cv, "RGBA")
    d.line([(0, GROUND * R), (300 * R, GROUND * R)], fill=(18, 20, 22, 60), width=2 * R)
    h = -p['dy']
    sw = (60 - h * 0.8) * R; sh_ = (9 - h * 0.12) * R
    cx = (ROOT_X + p['dx']) * R
    d.ellipse([cx - sw / 2, GROUND * R - sh_ / 2, cx + sw / 2, GROUND * R + sh_ / 2], fill=(18, 20, 22, 70))
    a = parts['armor']
    if p.get('ghost'):
        for k, al in ((2, .12), (1, .22)):
            gp = pose(kind, anim, max(0, t - k * .03)); gm = matrices(parts, gp)
            cv.alpha_composite(silhouette(parts, gm, ORANGE, al))
    blit(cv, parts['pack'], m['pack'])
    blit(cv, a['legF'], m['legF'])
    # far-side arm: behind the torso, mirrored + darkened so it reads as the other arm, not a copy of the near one
    blit(cv, far_side(a['armF']), m['armF'])
    blit(cv, far_side(parts['hand']), m['handF'])
    blit(cv, a['legN'], m['legN'])
    blit(cv, a['torso'], m['torso'])
    blit(cv, parts['head'], m['head'])
    # tip trail: drawn BEHIND arm/blade so the dagger itself always stays visible in the hand
    if p['fx']:
        pts = []
        for k in range(10):
            tt = t - k * 0.009
            pts.append(tip_of(parts, kind, anim, max(0, tt)))
        for i in range(len(pts) - 1):
            al = int(230 * (1 - i / 9)); wd = int((5 - i * .4) * R * (0.9 if kind != "spear" else 0.55))
            d.line([pts[i], pts[i + 1]], fill=(255, 120, 40, al), width=max(2, wd))
            d.line([pts[i], pts[i + 1]], fill=(255, 255, 255, int(al * .8)), width=max(1, wd // 3))
    blit(cv, a['arm'], m['armN'])
    blit(cv, parts['wpn'], m['wpn'])
    blit(cv, parts['hand'], m['hand'])
    if p.get('glint'):
        tx, ty = tip_of(parts, kind, anim, t); ph = math.sin(math.pi * ((t - .08) / .12 if t < .3 else (t - .54) / .08))
        star(d, tx, ty, 9 * R * ph, 2 * R, (255, 255, 255, 240)); star(d, tx, ty, 5 * R * ph, 1 * R, ORANGE + (255,))
    if anim == "combo":                                  # hit spark: white core + radiating rays, bigger on the finisher
        for i, h in enumerate(COMBO_HITS):
            k = (t - h) / .08
            if 0 <= k < 1:
                big = 1.7 if i == 2 else 1.0; fade = 1 - k
                hx, hy = tip_of(parts, kind, anim, h)
                core = (7 * (1 - k * .6)) * R * big
                d.ellipse([hx - core, hy - core, hx + core, hy + core], fill=(255, 255, 255, int(240 * fade)))
                for j in range(8):
                    ang = math.radians(j * 45 + 22 * i + (0 if j % 2 else 10))
                    r0 = (6 + 14 * k) * R * big; r1 = r0 + (16 if j % 2 == 0 else 9) * R * big * fade
                    c_, s_ = math.cos(ang), math.sin(ang)
                    d.line([(hx + c_ * r0, hy + s_ * r0), (hx + c_ * r1, hy + s_ * r1)], fill=ORANGE + (int(255 * fade),), width=max(1, int(3 * R * fade * big)))
                if i == 2:
                    r_ = (10 + 34 * k) * R
                    d.ellipse([hx - r_, hy - r_ * .55, hx + r_, hy + r_ * .55], outline=(255, 255, 255, int(200 * fade)), width=max(1, int(2 * R * fade)))
    if p.get('dust'):
        for k in range(3):
            r_ = (4 + k * 3) * R; x_ = (ROOT_X + p['dx'] - 26 - k * 12) * R
            d.ellipse([x_ - r_, (GROUND - 3) * R - r_, x_ + r_, (GROUND - 3) * R + r_], fill=(196, 199, 193, 200))
    cv = cv.crop((20 * R, 50 * R, 260 * R, 250 * R))
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
    for anim, n in (("idle", 14), ("walk", 14), ("attack", 20), ("combo", 34)):
        fr = sheet(anim, n, sets, f"{out}/rig_{anim}.gif", dur={"idle": 90, "combo": 40}.get(anim, 70))
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
        save("torso", ar['torso']); save("arm", ar['arm']); save("leg_near", ar['legN']); save("leg_far", ar['legF']); save("hand", hand())
        save("head", sp['head']()); save("pack", sp['pack']()); save("weapon", sp['wpn']())
        parts["arm"]["hand"] = [parts["arm"]["pivot"][0], parts["arm"]["pivot"][1] + ar['arm'].img.size[1] / R - 11 - ar['arm'].pivot[1] / R]
        json.dump(dict(scale=4, weapon_class=sp['kind'], shoulder=list(ar['shoulder']), shoulderF=list(ar['shoulderF']),
                       parts=parts), open(os.path.join(d, "parts.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
