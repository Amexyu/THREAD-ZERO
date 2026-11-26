# 卡43：骨骼跑步循环原型（Python 版，和游戏里 LayeredRigView 同一套矩阵约定）。
# 腿：大腿 / 小腿 / 鞋三段，脚踝位置按跑步轨迹算，再用两段 IK 解出胯和膝盖角度；手臂：上臂 / 前臂 / 手，手肘弯。
import sys, os, json, math, numpy as np
from PIL import Image, ImageDraw
D = sys.argv[1]; m = json.load(open(os.path.join(D, 'parts.json'))); SC = m['scale']
def T(x, y): return np.array([[1, 0, x], [0, 1, y], [0, 0, 1.]])
def Rm(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1.]])
def S(x, y): return np.array([[x, 0, 0], [0, y, 0], [0, 0, 1.]])
V = lambda a: np.array(a, float)
P = {k: dict(img=Image.open(os.path.join(D, e['file'])).convert('RGBA'), pivot=V(e['pivot']), child=V(e['hand']) if 'hand' in e else None, tip=V(e['tip']) if 'tip' in e else None) for k, e in m['parts'].items()}
rg = m['rig']; hip = V(rg['hip']); headOff = V(rg['head']); legN = V(rg['leg_near']); legF = V(rg['leg_far']); sh = V(m['shoulder']); shF = V(m['shoulderF'])
def vec(k): return P[k]['child'] - P[k]['pivot']
def ang(v): return math.degrees(math.atan2(v[0], v[1]))   # 和「竖直向下」的夹角，往前为正
CYCLE = 80.0; ST = .36
def Sm(x): x = max(0., min(1., x)); return x * x * (3 - 2 * x)
def toe_contact(ft):
    """鞋底最前面那个着地点（鞋的局部坐标，单位）：平放时最低的那一圈里最靠前的点。"""
    pt = P[ft]; a = np.asarray(pt['img'])[..., 3] > 128; ys, xs = np.nonzero(a)
    X = xs / SC - pt['pivot'][0]; Y = ys / SC - pt['pivot'][1]; low = Y.max(); sel = Y >= low - .35
    i = np.argmax(np.where(sel, X, -1e9)); return np.array([X[i], low])
TOE = {}; SIL = {}
for _ft in ('foot', 'foot_far'):
    _pt = P[_ft]; _a = np.asarray(_pt['img'])[..., 3] > 128; _ys, _xs = np.nonzero(_a); _k = max(1, len(_xs) // 600)
    SIL[_ft] = np.stack([_xs[::_k] / SC - _pt['pivot'][0], _ys[::_k] / SC - _pt['pivot'][1]], 1)
def rot(v, f):   # 和 Rm 一样的转法：正 = 往前转（鞋尖上翘）
    a = math.radians(f); c, s = math.cos(a), math.sin(a); return np.array([c * v[0] + s * v[1], -s * v[0] + c * v[1]])
def leg_targets(u, side):
    """一条腿在跑步循环 u 时：脚踝相对 root 的目标（单位，y 向下，地面 y = 0）和鞋的转角（相对平放，正 = 鞋尖上翘）。
    着地期：鞋尖着地点跟着地面往后走（不打滑），后半段绕鞋尖抬脚跟；摆动期：脚踝走一条往后踢再往前送的弧线。"""
    ft = 'foot' if side == 0 else 'foot_far'
    if ft not in TOE: TOE[ft] = toe_contact(ft)
    toe = TOE[ft]; hipj = hip + (legN if side == 0 else legF)
    Dst = ST * CYCLE; xc = hipj[0] + 26.0
    def ankle_at(cx, f):
        a = np.array([cx, 0.0]) + rot(-toe, f)
        # 鞋绕鞋尖转的时候，鞋尖前面那一点圆头会压到地下：量一下转完以后最低点，压下去多少就抬多少
        pts = SIL[ft]; w = np.stack([rot(p, f) for p in pts]) + a; low = w[:, 1].max()
        if low > 0: a[1] -= low
        return a
    if u < ST:
        cx = xc - Dst * (u / ST); f = 0 if u < ST * .45 else -32 * Sm((u - ST * .45) / (ST * .55))
        return ankle_at(cx, f), f, hipj
    sw = (u - ST) / (1 - ST)
    ank0 = ankle_at(xc - Dst, -32); ank1 = ankle_at(xc, 0)
    e = Sm((sw - .12) / .8)
    lift = 9 * math.exp(-((sw - .34) / .22) ** 2) + 4 * math.sin(math.pi * sw)
    ank = np.array([ank0[0] + (ank1[0] - ank0[0]) * e, ank0[1] + (ank1[1] - ank0[1]) * sw - lift])
    f = -32 - 13 * math.sin(math.pi * min(1, sw / .5)) if sw < .5 else -45 * (1 - Sm((sw - .5) / .4)) + 12 * math.sin(math.pi * (sw - .5) / .5)
    return ank, f, hipj
def ik(target, hipj, th, sh_):
    v1 = vec(th); v2 = vec(sh_); L1, L2 = np.linalg.norm(v1), np.linalg.norm(v2)
    d = target - hipj; dist = min(max(np.linalg.norm(d), abs(L1 - L2) + .01), L1 + L2 - .01)
    phi = math.degrees(math.atan2(d[0], d[1]))
    al = math.degrees(math.acos((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist))); be = math.degrees(math.acos((L2 * L2 + dist * dist - L1 * L1) / (2 * L2 * dist)))
    t_abs = phi + al; s_abs = phi - be
    return t_abs - ang(v1), s_abs - t_abs - ang(v2) + ang(v1), t_abs, s_abs   # 胯角、膝角（相对大腿）
def pose_run(u):
    dy = 3.0 + 1.8 * math.cos(4 * math.pi * (u - ST / 2)); lean = 9 + 1.5 * math.sin(4 * math.pi * u)
    p = dict(dx=0, dy=dy, lean=lean, head=-5, A=-34 * math.cos(2 * math.pi * u), Af=34 * math.cos(2 * math.pi * u))
    p['E'] = 70 + 25 * (p['A'] + 34) / 68; p['Ef'] = 70 + 25 * (p['Af'] + 34) / 68
    legs = {}
    for side, uu in ((0, u), (1, (u + .5) % 1)):
        ank, f, hj = leg_targets(uu, side)
        ank_local = ank - np.array([0, dy])   # root 往下挪了 dy，目标换回 root 坐标
        th = 'thigh' if side == 0 else 'thigh_far'; sn = 'shin' if side == 0 else 'shin_far'
        a_th, a_kn, t_abs, s_abs = ik(ank_local, hj, th, sn)
        a_kn = max(-110.0, min(2.0, a_kn)); legs[side] = (a_th, a_kn, f - (a_th + a_kn))
    p['legs'] = legs; return p
def pose_idle(t):
    ph = 2 * math.pi * t
    return dict(dx=0, dy=-1.5 * (.5 - .5 * math.cos(ph)), lean=.5 + 1.2 * math.sin(ph - .5), head=-2.5 * math.sin(ph - 1), A=1 + 3 * math.sin(ph - .7), Af=-1 - 3 * math.sin(ph - .7), E=6, Ef=6, legs={0: (0, 0, 0), 1: (0, 0, 0)})
def build(p):
    root = T(p['dx'], p['dy']); b = {}
    b['torso'] = root @ T(*hip) @ Rm(-p['lean'])
    b['head'] = b['torso'] @ T(*headOff) @ Rm(-p['head'])
    b['upper'] = b['torso'] @ T(*sh) @ Rm(p['A']); b['fore'] = b['upper'] @ T(*vec('upper')) @ Rm(p['E']); b['hand'] = b['fore'] @ T(*vec('fore'))
    b['upper_far'] = b['torso'] @ T(*shF) @ Rm(p['Af']); b['fore_far'] = b['upper_far'] @ T(*vec('upper_far')) @ Rm(p['Ef']); b['hand_far'] = b['fore_far'] @ T(*vec('fore_far'))
    for side, (a_th, a_kn, a_ft) in p['legs'].items():
        sfx = '' if side == 0 else '_far'; off = legN if side == 0 else legF
        b['thigh' + sfx] = root @ T(*(hip + off)) @ Rm(a_th); b['shin' + sfx] = b['thigh' + sfx] @ T(*vec('thigh' + sfx)) @ Rm(a_kn); b['foot' + sfx] = b['shin' + sfx] @ T(*vec('shin' + sfx)) @ Rm(a_ft)
    return b
ORDER = ['thigh_far', 'shin_far', 'foot_far', 'upper_far', 'fore_far', 'hand_far', 'thigh', 'shin', 'foot', 'torso', 'head', 'upper', 'fore', 'hand']
FAR = {'thigh_far', 'shin_far', 'foot_far', 'upper_far', 'fore_far', 'hand_far'}
PX = 3.0; CW, CH = 420, 520
def render(p, ground_off=0):
    cv = Image.new('RGBA', (CW, CH), (236, 233, 224, 255)); d = ImageDraw.Draw(cv)
    gy = CH - 50; d.line([(0, gy), (CW, gy)], fill=(110, 110, 110, 255), width=2)
    for k in range(-2, 12): x = (k * 40 - ground_off * PX) % (CW + 40) - 20; d.line([(x, gy + 2), (x - 14, gy + 14)], fill=(150, 150, 150, 255), width=2)
    scr = T(CW / 2 - 20, gy) @ S(PX, PX); b = build(p)
    for k in ORDER:
        pt = P[k]; M = scr @ b[k] @ T(-pt['pivot'][0], -pt['pivot'][1]) @ S(1 / SC, 1 / SC); inv = np.linalg.inv(M)
        img = pt['img']
        if k in FAR: a = np.asarray(img).astype(np.float32); a[..., :3] *= m.get('far_shade', .72); img = Image.fromarray(a.astype(np.uint8))
        cv.alpha_composite(img.transform((CW, CH), Image.AFFINE, data=tuple(inv[:2].flatten()), resample=Image.BICUBIC))
    return cv
def check(u, p, b):
    bad = []
    for side in (0, 1):
        a_th, a_kn, a_ft = p['legs'][side]
        if not (-45 <= a_th <= 80): bad.append('胯角 %.0f' % a_th)
        if not (-112 <= a_kn <= 3): bad.append('膝角 %.0f' % a_kn)
        if not (-70 <= a_ft <= 50): bad.append('鞋角 %.0f' % a_ft)
        sfx = '' if side == 0 else '_far'
        ank = (b['shin' + sfx] @ np.array([*vec('shin' + sfx), 1.]))[:2]; fp = (b['foot' + sfx] @ np.array([0, 0, 1.]))[:2]
        if np.linalg.norm(ank - fp) > .05: bad.append('鞋没接在小腿末端')
        uu = u if side == 0 else (u + .5) % 1
        if uu < ST:   # 着地：鞋底最低点贴地
            pt = P['foot' + sfx]; img = np.asarray(pt['img'])[..., 3] > 128; ys, xs = np.nonzero(img)
            pts = np.stack([xs / SC - pt['pivot'][0], ys / SC - pt['pivot'][1], np.ones(len(xs))])
            w = (b['foot' + sfx] @ pts); low = w[1].max()
            if abs(low - 0) > 1.2: bad.append('着地脚离地 %.1f' % low)
    return bad
if __name__ == '__main__':
    out = sys.argv[2]; N = 24; CPS = 1.84   # 每秒 1.84 个循环（游戏里跑动的速度 220 像素/秒 ÷ 每循环 80 单位 × 1.5 像素）
    allbad = []
    for i in range(N):
        p = pose_run(i / N); bad = check(i / N, p, build(p))
        if bad: allbad.append((i, bad))
    print('检查不合格的帧：', allbad if allbad else '无')
    frames = [render(pose_run((i % N) / N), ground_off=(i / N) * CYCLE).convert('RGB') for i in range(N * 3)]
    big = Image.new('RGB', (CW * 6, CH * 4), (255, 255, 255))
    for i in range(N): big.paste(frames[i], ((i % 6) * CW, (i // 6) * CH))
    big.save(out.replace('.gif', '_24帧.png'))
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=round(1000 / (CPS * N)), loop=0)
    frames[0].save(out.replace('.gif', '_慢放.gif'), save_all=True, append_images=frames[1:], duration=round(1000 / (CPS * N)) * 3, loop=0)
    sheet = Image.new('RGB', (CW * 8, CH)); [sheet.paste(frames[i * 3], (i * CW, 0)) for i in range(8)]; sheet.save(out.replace('.gif', '_sheet.jpg'), quality=85)
    print('ok', round(1000 / (CPS * N)))
