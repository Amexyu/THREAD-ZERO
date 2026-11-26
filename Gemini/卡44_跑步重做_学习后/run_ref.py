# 卡44：学了动画关键姿势和真人跑步数据以后，重做的跑步循环（Python 预览，矩阵约定和游戏里 LayeredRigView 一样）。
# 依据（详见项目文档 v0.45 学习笔记）：
#  1) 动画关键姿势：触地 → 下沉 → 交错 → 蹬地 → 腾空 → 另一只脚触地（一步），左右对称再一步，合成一个循环。
#  2) 真人慢跑：触地时大腿前摆约 25-30°、膝弯 20-25°、小腿接近竖直；支撑前段膝弯到约 45°（身体最低）；
#     蹬离时大腿后摆约 20-25°、膝弯约 20-25°；摆动期膝盖最大弯 90-105°（脚跟踢到屁股下），大腿最大前摆约 50-55°，
#     落地前大腿往回收一点；脚踝背屈最多约 25-30°，超过就靠抬脚跟。
#  3) 着地约占循环 36-40%，两次腾空；身体最低在支撑前段、最高在腾空中间；手臂和同侧腿反向，腾空时摆到最远，手肘约 90°。
import sys, os, json, math
import numpy as np
from PIL import Image, ImageDraw

D = sys.argv[1]; OUT = sys.argv[2]; MODE = sys.argv[3] if len(sys.argv) > 3 else 'check'
os.makedirs(OUT, exist_ok=True)
m = json.load(open(os.path.join(D, 'parts.json'))); SC = m['scale']
def T(x, y): return np.array([[1, 0, x], [0, 1, y], [0, 0, 1.]])
def Rm(a): a = math.radians(a); c, s = math.cos(a), math.sin(a); return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1.]])
def S(x, y): return np.array([[x, 0, 0], [0, y, 0], [0, 0, 1.]])
V = lambda a: np.array(a, float)
P = {}
for k, e in m['parts'].items():
    if k in ('arm', 'arm_far', 'leg_near', 'leg_far', 'pack'): continue
    P[k] = dict(img=Image.open(os.path.join(D, e['file'])).convert('RGBA'), pivot=V(e['pivot']), child=V(e['hand']) if 'hand' in e else None)
rg = m['rig']; HIP = V(rg['hip']); HEAD = V(rg['head']); OFF = {0: V(rg['leg_near']), 1: V(rg['leg_far'])}
SH = {0: V(m['shoulder']), 1: V(m['shoulderF'])}
SFX = {0: '', 1: '_far'}
def vec(k): return P[k]['child'] - P[k]['pivot']
def ang(v): return math.degrees(math.atan2(v[0], v[1]))      # 和竖直向下的夹角，往前为正
def rot(v, f):                                              # 和 Rm 同一个转法；正 = 往前转（鞋尖上翘）
    a = math.radians(f); c, s = math.cos(a), math.sin(a)
    v = np.asarray(v, float); return np.stack([c * v[..., 0] + s * v[..., 1], -s * v[..., 0] + c * v[..., 1]], -1)
L1 = float(np.linalg.norm(vec('thigh'))); L2 = float(np.linalg.norm(vec('shin')))
R1 = ang(vec('thigh')); R2 = ang(vec('shin'))
def Sm(x): x = max(0., min(1., x)); return x * x * (3 - 2 * x)

# ---------- 鞋：轮廓点、脚跟点、前掌点 ----------
def sil(k):
    pt = P[k]; a = np.asarray(pt['img'])[..., 3] > 128; ys, xs = np.nonzero(a)
    p = np.stack([(xs + .5) / SC - pt['pivot'][0], (ys + .5) / SC - pt['pivot'][1]], 1)
    return p[:: max(1, len(p) // 1500)]
SIL = {s: sil('foot' + SFX[s]) for s in (0, 1)}
def sole(s):
    p = SIL[s]; low = p[:, 1].max(); q = p[p[:, 1] >= low - .45]
    h = q[np.argmin(q[:, 0])].copy(); b = q[np.argmax(q[:, 0])].copy(); h[1] = b[1] = low
    return h, b
SOLE = {s: sole(s) for s in (0, 1)}
def lowest(s, f, ank): return float((rot(SIL[s], f) + ank)[:, 1].max())
def lift(s, f, ank):
    lo = lowest(s, f, ank)
    if lo > 0: ank = ank.copy(); ank[1] -= lo
    return ank

# ---------- 参数 ----------
FPS = 25; N = 18                      # 预览：每循环 18 帧（一步 9 帧；卡通小孩跑步常用一步 8-10 帧）
SPEED = 220 / 1.5                     # 游戏里跑动 220 像素/秒 = 146.7 单位/秒
STRIDE = SPEED * N / FPS              # 一个循环往前 105.6 单位（地面刻度按这个走，不打滑）
ST = .35                              # 着地占比（真人跑步 0.31-0.39）
W_FLAT, F_HS = .05, 9.0               # 触地时鞋尖上翘 9°，0.05 循环内放平
W_PUSH, F_TO, PUSH_POW = .14, -34.0, 1.2   # 着地后段绕前掌转，蹬离时鞋尖朝下 34°，越到最后转得越快（把身体往上送）
DMAX = 27.0                           # 脚踝最多背屈 27°，再多就抬脚跟
TH_IC = 20.0                          # 触地：大腿前摆 20°（落脚靠近身体下方，不刹车）
# 支撑腿膝弯（关键点）：触地 20° → 触地后马上下沉到 50°（一两帧的冲击）→ 交错 36° → 推地伸到最直 21° → 蹬离时已经开始弯
K_KEYS = [(0., 16.), (.075, 54.), (.16, 41.), (ST - .08, 24.), (ST, 20.)]

def pchip_slopes(xs, ys):
    n = len(xs); h = np.diff(xs); dl = np.diff(ys) / h; m_ = np.zeros(n)
    for i in range(1, n - 1):
        if dl[i - 1] * dl[i] <= 0: m_[i] = 0
        else:
            w1 = 2 * h[i] + h[i - 1]; w2 = h[i] + 2 * h[i - 1]; m_[i] = (w1 + w2) / (w1 / dl[i - 1] + w2 / dl[i])
    if n > 2: m_[0] = dl[0]; m_[-1] = dl[-1]
    return m_
def herm(xs, ys, ms, x):
    i = int(np.clip(np.searchsorted(xs, x, side='right') - 1, 0, len(xs) - 2)); h = xs[i + 1] - xs[i]; t = (x - xs[i]) / h
    h00 = 2 * t ** 3 - 3 * t ** 2 + 1; h10 = t ** 3 - 2 * t ** 2 + t; h01 = -2 * t ** 3 + 3 * t ** 2; h11 = t ** 3 - t ** 2
    return h00 * ys[i] + h10 * h * ms[i] + h01 * ys[i + 1] + h11 * h * ms[i + 1]
_kx = np.array([k[0] for k in K_KEYS]); _ky = np.array([k[1] for k in K_KEYS]); _km = pchip_slopes(_kx, _ky); _km[0] = 0.
def K_stance(w): return herm(_kx, _ky, _km, w)

def ik(target, hipj):
    d = target - hipj; dist = float(np.linalg.norm(d)); dist = min(max(dist, abs(L1 - L2) + 1e-3), L1 + L2 - 1e-3)
    phi = math.degrees(math.atan2(d[0], d[1]))
    al = math.degrees(math.acos(max(-1, min(1, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist)))))
    be = math.degrees(math.acos(max(-1, min(1, (L2 * L2 + dist * dist - L1 * L1) / (2 * L2 * dist)))))
    return phi + al, phi - be                            # 膝盖朝前：大腿、小腿的世界角

def ground(s, f, ank):                                    # 着地的鞋：最低点正好贴地（只上下挪）
    ank = ank.copy(); ank[1] -= lowest(s, f, ank); return ank

HIPJ_Y = HIP[1] + OFF[0][1]
def leg_from(s, w, f, G0_):
    """给定鞋角 f：脚踝位置 → 膝弯按曲线 → 胯高和大腿、小腿角。"""
    heel, ball = SOLE[s]; g = G0_ - STRIDE * w
    if f > 0 or w < W_FLAT: ank = ground(s, f, np.array([g, 0.]) - rot(heel, f))
    else: ank = ground(s, f, np.array([g + ball[0] - heel[0], 0.]) - rot(ball, f))
    K = K_stance(w); d = math.sqrt(L1 * L1 + L2 * L2 + 2 * L1 * L2 * math.cos(math.radians(K)))
    H = -ank[1] + math.sqrt(max(d * d - ank[0] ** 2, 1e-6))
    th, sh = ik(ank, np.array([0., -H]))
    return dict(dy=-H - HIPJ_Y, H=H, th=th, K=th - sh, A=f - (sh - R2), f=f)

def stance_solve(s, w, G0_=None):
    G0_ = G0 if G0_ is None else G0_
    if w < W_FLAT: return leg_from(s, w, F_HS * (1 - Sm(w / W_FLAT)), G0_)
    f_push = F_TO * (max(0., (w - W_PUSH) / (ST - W_PUSH)) ** PUSH_POW)
    r = leg_from(s, w, f_push, G0_)
    if r['A'] <= DMAX: return r
    lo, hi = -85., f_push                                 # 背屈超了：脚跟抬到刚好 DMAX（A 随 f 单调）
    for _ in range(40):
        mid = (lo + hi) / 2
        if leg_from(s, w, mid, G0_)['A'] > DMAX: hi = mid
        else: lo = mid
    return leg_from(s, w, lo, G0_)

def find_G0():
    lo, hi = -10., 40.
    for _ in range(50):
        mid = (lo + hi) / 2
        if stance_solve(0, 0., mid)['th'] < TH_IC: lo = mid
        else: hi = mid
    return (lo + hi) / 2
G0 = find_G0()
H_TD = stance_solve(0, 0.)['H']

def deriv(fn, x, h=1e-4): return (fn(x + h) - fn(x - h)) / (2 * h)

# ---------- 摆动期：关键姿势 + 平滑插值，两端的值和速度都接上着地期 ----------
SW_TH = [(.47, -12), (.57, 2), (.66, 20), (.76, 42), (.85, 52), (.93, 42)]   # 大腿世界角（往前为正）
SW_K = [(.385, 38), (.40, 50), (.45, 72), (.57, 108), (.66, 116), (.74, 113), (.80, 104), (.86, 78), (.93, 42)]  # 膝弯：小腿收着一直到另一条腿蹬离才往前甩
SW_A = [(.40, 14), (.45, 6), (.50, -3), (.57, -5), (.66, 0), (.76, 5), (.86, 6), (.93, 4)]        # 鞋相对小腿（背屈为正）；Q 版鞋大，摆动时不能绷太直
def build_swing():
    sw = {}
    for s in (0, 1):
        a0 = stance_solve(s, ST - 1e-6); a1 = stance_solve(s, 0.0); curves = {}
        for key, keys in (('th', SW_TH), ('K', SW_K), ('A', SW_A)):
            xs = np.array([ST] + [k[0] for k in keys] + [1.0]); ys = np.array([a0[key]] + [k[1] for k in keys] + [a1[key]], float)
            ms = pchip_slopes(xs, ys)
            ms[0] = deriv(lambda w: stance_solve(s, w)[key], ST - 3e-3, 2e-3)
            ms[-1] = deriv(lambda w: stance_solve(s, w)[key], 3e-3, 2e-3)
            curves[key] = (xs, ys, ms)
        sw[s] = curves
    return sw
SWING = build_swing()

def _dy(s, w): return stance_solve(s, w)['dy']
_FL = dict(p0=_dy(0, ST - 1e-6), m0=deriv(lambda w: _dy(0, w), ST - 3e-3, 2e-3), p1=_dy(0, 0.), m1=deriv(lambda w: _dy(0, w), 3e-3, 2e-3))
def root_dy(u):
    w = u % .5
    if w < ST: return _dy(0 if (u % 1) < .5 else 1, w)
    return herm(np.array([ST, .5]), np.array([_FL['p0'], _FL['p1']]), np.array([_FL['m0'], _FL['m1']]), w)
def leg_angles(s, u):
    w = (u - (0 if s == 0 else .5)) % 1
    if w < ST: a = stance_solve(s, w); return a['th'], a['K'], a['A'], True
    c = SWING[s]; return tuple(herm(*c[k], w) for k in ('th', 'K', 'A')) + (False,)

# ---------- 上身和手臂 ----------
ARM_MID, ARM_AMP, EL_MID, EL_AMP = -23.0, 35.0, 81.0, 19.0   # 上臂世界角 前 +12 / 后 -58（相对上身：前屈 25°、后伸 45°）；手肘 前 100° / 后 62°
def upper(u):
    lean = 13 + 2.0 * math.cos(4 * math.pi * (u - .12))          # 下沉时最前倾，腾空时最直
    headw = 3 + 1.2 * math.cos(4 * math.pi * (u - .18))          # 头基本看前方，比上身晚一点
    arm = {}
    for s, ph in ((0, .44), (1, .94)):                            # 近侧手：近侧腿在后面腾空时摆到最前
        c = math.cos(2 * math.pi * (u - ph)); arm[s] = (ARM_MID + ARM_AMP * c, EL_MID + EL_AMP * c)
    return lean, headw, arm

def pose(u):
    dy = root_dy(u); lean, headw, arm = upper(u)
    p = dict(u=u, dy=dy, lean=lean, head=headw - lean, legs={}, arms={})
    for s in (0, 1):
        th, K, A, st = leg_angles(s, u)
        a_th = th - R1; a_kn = (th - K) - R2 - a_th; p['legs'][s] = (a_th, a_kn, A, st, th, K)
        Aw, E = arm[s]; p['arms'][s] = (Aw + lean, E)
    return p

def build(p):
    root = T(0, p['dy']); b = {}
    b['torso'] = root @ T(*HIP) @ Rm(-p['lean'])
    b['head'] = b['torso'] @ T(*HEAD) @ Rm(-p['head'])
    for s in (0, 1):
        x = SFX[s]; A, E = p['arms'][s]
        b['upper' + x] = b['torso'] @ T(*SH[s]) @ Rm(A); b['fore' + x] = b['upper' + x] @ T(*vec('upper' + x)) @ Rm(E); b['hand' + x] = b['fore' + x] @ T(*vec('fore' + x))
        a_th, a_kn, a_ft = p['legs'][s][:3]
        b['thigh' + x] = root @ T(*(HIP + OFF[s])) @ Rm(a_th); b['shin' + x] = b['thigh' + x] @ T(*vec('thigh' + x)) @ Rm(a_kn); b['foot' + x] = b['shin' + x] @ T(*vec('shin' + x)) @ Rm(a_ft)
    return b

ORDER = ['upper_far', 'fore_far', 'hand_far', 'thigh_far', 'shin_far', 'foot_far', 'thigh', 'shin', 'foot', 'torso', 'head', 'upper', 'fore', 'hand']
USE_SKIN = True
KNEE_R, ELBOW_R = 5.2, 4.6      # 关节过渡区半径（单位）：大一点弯出来是圆的，不是折角
FILLET = True                   # 关节用圆角弯（外侧圆、内侧收成折痕）
ORDER_SKIN = ['arm_far', 'hand_far', 'leg_far', 'foot_far', 'leg', 'foot', 'torso', 'head', 'arm', 'hand']
import skin as _skin
_SM = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '条带', 'strips.json')))
_SD = os.path.join(os.path.dirname(os.path.abspath(__file__)), '条带')
STRIPS = {}
for _s in (0, 1):
    _sh = 1.0 if _s == 0 else m.get('far_shade', .72)
    STRIPS['leg' + SFX[_s]] = _skin.Strip(os.path.join(_SD, _SM['leg']['file']), _SM['leg']['joints'], _SM['scale'], r0=3.0, r1=KNEE_R, shade=_sh)
    STRIPS['arm' + SFX[_s]] = _skin.Strip(os.path.join(_SD, _SM['sleeve']['file']), _SM['sleeve']['joints'], _SM['scale'], r0=2.6, r1=ELBOW_R, cols=8, rows=60, shade=_sh)
def skin_layer(k, b, scr, p):
    s = 1 if k.endswith('_far') else 0; x = SFX[s]; st = STRIPS[k]
    if k.startswith('leg'):
        Mpar = T(0, p['dy']) @ T(*(HIP + OFF[s])); M1 = b['thigh' + x]; M2 = b['shin' + x]
    else:
        Mpar = b['torso'] @ T(*SH[s]); M1 = b['upper' + x]; M2 = b['fore' + x]
    if FILLET: V = _skin.deform_fillet(st, scr @ Mpar, scr @ M1, scr @ M2, st.r0, st.r1)
    else: V = st.deform(scr @ Mpar, scr @ M1, scr @ M2)
    lay = st.render(V, (CW, CH), PX)
    a = lay[..., 3:4] / 255.; rgb = np.where(a > 1e-4, lay[..., :3] / np.maximum(a, 1e-4), 0)   # 预乘 → 普通
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), lay[..., 3]]).astype(np.uint8), 'RGBA')
FAR = {k for k in ORDER if k.endswith('_far')}
_FARIMG = {}
def img_of(k):
    if k in FAR:
        if k not in _FARIMG:
            a = np.asarray(P[k]['img']).astype(np.float32); a[..., :3] *= m.get('far_shade', .72); _FARIMG[k] = Image.fromarray(a.astype(np.uint8))
        return _FARIMG[k]
    return P[k]['img']
PX = 3.0; CW, CH = 480, 520
def render(p, skeleton=False):
    cv = Image.new('RGBA', (CW, CH), (236, 233, 224, 255)); d = ImageDraw.Draw(cv)
    gy = CH - 50; d.line([(0, gy), (CW, gy)], fill=(110, 110, 110, 255), width=2)
    off = (p['u'] % 1) * STRIDE * PX
    for k in range(-2, 16): x = (k * 40 - off) % (CW + 40) - 20; d.line([(x, gy + 2), (x - 14, gy + 14)], fill=(150, 150, 150, 255), width=2)
    scr = T(CW / 2 - 10, gy) @ S(PX, PX); b = build(p)
    for k in (ORDER_SKIN if USE_SKIN else ORDER):
        if k in STRIPS and USE_SKIN: cv.alpha_composite(skin_layer(k, b, scr, p)); continue
        pt = P[k]; M = scr @ b[k] @ T(-pt['pivot'][0], -pt['pivot'][1]) @ S(1 / SC, 1 / SC); inv = np.linalg.inv(M)
        cv.alpha_composite(img_of(k).transform((CW, CH), Image.AFFINE, data=tuple(inv[:2].flatten()), resample=Image.BICUBIC))
    if skeleton:
        d = ImageDraw.Draw(cv)
        for s, col in ((1, (60, 120, 255, 255)), (0, (255, 60, 60, 255))):
            x = SFX[s]; pts = [scr @ b['thigh' + x] @ np.array([0, 0, 1.]), scr @ b['shin' + x] @ np.array([0, 0, 1.]), scr @ b['foot' + x] @ np.array([0, 0, 1.])]
            d.line([tuple(q[:2]) for q in pts], fill=col, width=3)
            for q in pts: d.ellipse([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], fill=col)
    return cv

# ---------- 检查 ----------
def foot_world(b, s):
    pts = np.c_[SIL[s], np.ones(len(SIL[s]))].T; return (b['foot' + SFX[s]] @ pts)[:2].T
def check_all(n=400):
    bad = []; prev = None; info = dict(dy=[], th={0: [], 1: []}, K={0: [], 1: []}, A={0: [], 1: []}, low={0: [], 1: []}, f={0: [], 1: []}, cx={0: [], 1: []})
    for i in range(n):
        u = i / n; p = pose(u); b = build(p); info['dy'].append(p['dy'])
        for s in (0, 1):
            a_th, a_kn, A, st, th, K = p['legs'][s]
            for k_, v_ in (('th', th), ('K', K), ('A', A)): info[k_][s].append(v_)
            if not (-38 <= th <= 62): bad.append((round(u, 3), s, '大腿角 %.1f' % th))
            if not (0 <= K <= 122): bad.append((round(u, 3), s, '膝弯 %.1f' % K))
            if not (-45 <= A <= DMAX + .5): bad.append((round(u, 3), s, '脚踝 %.1f' % A))
            fw = foot_world(b, s); low = fw[:, 1].max(); info['low'][s].append(low)
            w = (u - (0 if s == 0 else .5)) % 1
            if st:
                if abs(low) > .25: bad.append((round(u, 3), s, '着地鞋底离地 %.2f' % low))
            else:
                if low > .05: bad.append((round(u, 3), s, '摆动的鞋钻到地下 %.2f' % low))
                if ST + .05 < w < .96 and low > -1.0: bad.append((round(u, 3), s, '鞋擦地 %.2f' % low))
        if prev is not None:
            for s in (0, 1):
                for j in range(3):
                    if abs(p['legs'][s][j] - prev['legs'][s][j]) > 5: bad.append((round(u, 3), s, '角度跳变 %d %.1f' % (j, p['legs'][s][j] - prev['legs'][s][j])))
        prev = p
    for i in range(n):
        u = i / n; w0 = u % .5
        if ST + .03 < w0 < .47 and not (info['low'][0][i] < -.3 and info['low'][1][i] < -.3): bad.append((round(u, 3), '-', '腾空期有脚没离地'))
    return bad, info

# 不打滑检查：着地期鞋上任意一点（取着地时的最低点）在世界里不动
def slip_check(s, n=200):
    w0 = W_FLAT + .005; u0 = w0 + (0 if s == 0 else .5)
    b = build(pose(u0)); fw = foot_world(b, s); idx = int(np.argmax(fw[:, 1])); err = 0.
    for i in range(n):
        w = w0 + (W_PUSH - w0) * i / (n - 1); u = w + (0 if s == 0 else .5)
        pt = foot_world(build(pose(u)), s)[idx]; ground_x = pt[0] + STRIDE * (w - w0)   # 加回地面往后走的量 = 世界坐标
        err = max(err, abs(ground_x - fw[idx][0]))
    return err

if __name__ == '__main__':
    print('腿 %.2f/%.2f 站立胯高 %.2f | 触地胯高 %.2f 脚跟落点 %+.2f | 步长 %.1f/循环 着地 %.0f%% | 鞋跟 %s 前掌 %s' % (L1, L2, -HIPJ_Y, H_TD, G0, STRIDE, ST * 100, SOLE[0][0].round(2), SOLE[0][1].round(2)))
    bad, info = check_all(); dy = np.array(info['dy'])
    print('身体起伏 %.2f（%.1f%% 腿长）最低 u=%.3f 最高 u=%.3f | 着地不打滑误差 %.3f/%.3f' % (np.ptp(dy), np.ptp(dy) / 43.4 * 100, np.argmax(dy) / 400, np.argmin(dy) / 400, slip_check(0), slip_check(1)))
    print(' u     胯高  | 近侧: 大腿  膝   踝   鞋最低 | 远侧鞋最低')
    for i in range(0, 400, 10):
        u = i / 400; print('%.3f %6.2f | %s %6.1f %5.1f %5.1f %6.2f | %6.2f' % (u, -dy[i] - HIPJ_Y, '着' if (u % 1) < ST else '摆', info['th'][0][i], info['K'][0][i], info['A'][0][i], info['low'][0][i], info['low'][1][i]))
    from collections import defaultdict
    g = defaultdict(list)
    for b_ in bad: g[(b_[1], b_[2].split(' ')[0])].append(b_[0])
    print('检查不合格：', '无' if not bad else '')
    for k_, v_ in g.items(): print('   ', k_, '相位 %.3f..%.3f' % (min(v_), max(v_)), len(v_))

# ---------- 出图 ----------
def font(sz):
    from PIL import ImageFont
    return ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', sz, index=2)
KEY = [('触地', 0.0), ('下沉', .075), ('交错', .18), ('蹬地', ST - .01), ('腾空', .42)]
def outputs():
    frames = [render(pose(i / N)).convert('RGB') for i in range(N)]
    for i, fr in enumerate(frames): fr.save(os.path.join(OUT, 'f%02d.png' % i))
    seq = frames * 3
    seq[0].save(os.path.join(OUT, '跑步_重做_原速.gif'), save_all=True, append_images=seq[1:], duration=int(1000 / FPS), loop=0)
    seq[0].save(os.path.join(OUT, '跑步_重做_慢放.gif'), save_all=True, append_images=seq[1:], duration=int(1000 / FPS) * 3, loop=0)
    sheet = Image.new('RGB', (CW * 6, CH * 3), (255, 255, 255))
    for i, fr in enumerate(frames): sheet.paste(fr, ((i % 6) * CW, (i // 6) * CH))
    d = ImageDraw.Draw(sheet); f = font(22)
    for i in range(N): d.text(((i % 6) * CW + 10, (i // 6) * CH + 8), '第 %d 帧' % (i + 1), fill=(60, 60, 60), font=f)
    sheet.save(os.path.join(OUT, '跑步_重做_18帧.png'))
    # 带骨架线的关键姿势
    ks = Image.new('RGB', (CW * len(KEY), CH + 50), (255, 255, 255)); d = ImageDraw.Draw(ks); f = font(30)
    for j, (nm, u) in enumerate(KEY):
        ks.paste(render(pose(u), skeleton=True).convert('RGB'), (j * CW, 50)); d.text((j * CW + CW // 2 - 30, 6), nm, fill=(30, 30, 30), font=f)
    ks.save(os.path.join(OUT, '跑步_重做_关键姿势.png'))
    print('出图完成', OUT)
if MODE == 'render': outputs()
