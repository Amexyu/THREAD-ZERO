# 卡40：照 LayeredRigView.Build 的骨骼算法在本地拼出待机 / 走路的几帧，先看部件拼得对不对（不进游戏）。
import sys, os, json, math, numpy as np
from PIL import Image, ImageDraw, ImageFont
D = sys.argv[1] if len(sys.argv) > 1 else '部件'; OUT = sys.argv[2] if len(sys.argv) > 2 else '切图v2/_rig_preview.png'
m = json.load(open(os.path.join(D, 'parts.json'))); SC = m['scale']
def T(x, y): return np.array([[1, 0, x], [0, 1, y], [0, 0, 1.]])
def Rm(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1.]])
def S(x, y): return np.array([[x, 0, 0], [0, y, 0], [0, 0, 1.]])
def V(a): return np.array(a, float)
parts = {}
for k, e in m['parts'].items():
    parts[k] = dict(img=Image.open(os.path.join(D, e['file'])).convert('RGBA'), pivot=V(e['pivot']), hand=V(e['hand']) if 'hand' in e else None)
rg = m['rig']; hip = V(rg['hip']); headOff = V(rg['head']); packOff = V(rg['pack']); legN = V(rg['leg_near']); legF = V(rg['leg_far'])
sh = V(m['shoulder']); shF = V(m['shoulderF'])
handPt = parts['arm']['hand'] - parts['arm']['pivot']; handPtF = parts['arm_far']['hand'] - parts['arm_far']['pivot']
def Sm(x): x = max(0., min(1., x)); return x * x * (3 - 2 * x)
def pose(anim, t):
    ph = 2 * math.pi * t; p = dict(dx=0, dy=0, sx=1, sy=1, tilt=0, lean=1, head=0, pack=0, A=8, Af=-8, L=0, Lf=0, shx=0)
    if anim == 'idle':
        p['dy'] = -5 * (.5 - .5 * math.cos(ph)); sq = math.cos(ph)
        p['sy'] = 1 - .04 * sq; p['sx'] = 1 + .035 * sq; p['tilt'] = 1.6 * math.sin(ph); p['lean'] = .5 + 1.2 * math.sin(ph - .5)
        p['head'] = -2.5 * math.sin(ph - 1); p['pack'] = -2 * math.sin(ph - 1.6); p['A'] = 1 + 3 * math.sin(ph - .7); p['Af'] = -1 - 3 * math.sin(ph - .7)
    else:
        sv = abs(math.sin(ph)); p['dy'] = -9 * sv; k = Sm((sv - .15) / .45)
        p['sy'] = .965 + .06 * k; p['sx'] = 2 - p['sy']; p['tilt'] = 3.2 * math.sin(ph); p['lean'] = 1
        p['head'] = -4 * math.sin(ph - 1); p['pack'] = -4 * math.sin(ph - 1.6)
        p['L'] = 7 * math.sin(ph) - 3 * sv; p['Lf'] = -7 * math.sin(ph) - 3 * sv
        p['A'] = -6 * math.sin(ph - .6) + 1; p['Af'] = 6 * math.sin(ph - .6) - 1
    if anim == 'reach': p.update(A=88, Af=30, lean=8)   # 手臂抬平（看拳头方向）
    return p
def build(p):
    root = T(p['dx'], p['dy']) @ Rm(-p['tilt']) @ S(p['sx'], p['sy'])
    b = {}
    b['torso'] = root @ T(*hip) @ Rm(-p['lean'])
    b['head'] = b['torso'] @ T(*headOff) @ Rm(-p['head']) @ S(1 / p['sx'], 1 / p['sy'])
    b['pack'] = b['torso'] @ T(*packOff) @ Rm(-p['pack'])
    b['arm'] = b['torso'] @ T(sh[0] + p['shx'], sh[1]) @ Rm(p['A'])
    b['arm_far'] = b['torso'] @ T(*shF) @ Rm(p['Af'])
    b['hand'] = b['arm'] @ T(*handPt); b['hand_far'] = b['arm_far'] @ T(*handPtF)
    aN, aF = (p['L'], p['Lf']) if legN[0] >= legF[0] else (p['Lf'], p['L'])
    b['leg_near'] = root @ T(*(hip + legN)) @ Rm(aN); b['leg_far'] = root @ T(*(hip + legF)) @ Rm(aF)
    return b
PX = 2.6; CW, CH = 360, 430
def render(anim, t):
    cv = Image.new('RGBA', (CW, CH), (236, 233, 224, 255)); d = ImageDraw.Draw(cv)
    d.line([(0, CH - 40), (CW, CH - 40)], fill=(120, 120, 120, 255), width=2)
    scr = T(CW / 2, CH - 40) @ S(PX, PX)
    b = build(pose(anim, t))
    for k in ['pack', 'leg_far', 'arm_far', 'hand_far', 'leg_near', 'torso', 'head', 'arm', 'hand']:
        pt = parts[k]; M = scr @ b[k] @ T(-pt['pivot'][0], -pt['pivot'][1]) @ S(1 / SC, 1 / SC)
        inv = np.linalg.inv(M)
        img = pt['img']
        if k in ('arm_far', 'hand_far'):
            a = np.asarray(img).astype(np.float32); a[..., :3] *= .62; img = Image.fromarray(a.astype(np.uint8))
        lay = img.transform((CW, CH), Image.AFFINE, data=tuple(inv[:2].flatten()), resample=Image.BICUBIC)
        cv.alpha_composite(lay)
    return cv
frames = [('idle', 0), ('idle', .5), ('walk', 0), ('walk', .25), ('walk', .5), ('walk', .75), ('reach', 0)]
S2 = Image.new('RGBA', (CW * len(frames), CH + 40), (255, 255, 255, 255)); dd = ImageDraw.Draw(S2)
f = ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', 22)
for i, (a, t) in enumerate(frames):
    S2.paste(render(a, t), (i * CW, 40)); dd.text((i * CW + 10, 6), f'{a} t={t}', fill=(0, 0, 0, 255), font=f)
S2.convert('RGB').save(OUT); print('ok', S2.size)
