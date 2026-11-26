# 卡48：攻击动作求解器。动作用「关键时刻 + 值」写（秒），每个通道单独插值（单调三次，不过冲）；
# 腿用 IK（脚钉在地上 / 迈步），手用 IK（手腕位置用肩膀为圆心的角度 + 伸出去多远来写，走弧线），
# 武器角 = 前臂 + 手腕握持角 θ（锤握 45°～105°）。导出成游戏的姿势通道（和 Build3 同一套约定）。
import math, numpy as np
from rig import *

def pchip_slopes(xs, ys):
    n = len(xs); h = np.diff(xs); dl = np.diff(ys) / h; m_ = np.zeros(n)
    for i in range(1, n - 1):
        if dl[i - 1] * dl[i] <= 0: m_[i] = 0
        else:
            w1 = 2 * h[i] + h[i - 1]; w2 = h[i] + 2 * h[i - 1]; m_[i] = (w1 + w2) / (w1 / dl[i - 1] + w2 / dl[i])
    return m_   # 两端速度 0（从静止开始、回到静止）
def herm(xs, ys, ms, x):
    if x <= xs[0]: return ys[0]
    if x >= xs[-1]: return ys[-1]
    i = int(np.clip(np.searchsorted(xs, x, side='right') - 1, 0, len(xs) - 2)); h = xs[i + 1] - xs[i]; t = (x - xs[i]) / h
    return (2*t**3 - 3*t**2 + 1) * ys[i] + (t**3 - 2*t**2 + t) * h * ms[i] + (-2*t**3 + 3*t**2) * ys[i + 1] + (t**3 - t**2) * h * ms[i + 1]
class Curve:
    def __init__(self, keys):
        k = sorted(keys); self.x = np.array([a for a, _ in k], float); self.y = np.array([b for _, b in k], float); self.m = pchip_slopes(self.x, self.y) if len(k) > 1 else np.zeros(1)
    def __call__(self, t): return float(self.y[0]) if len(self.x) == 1 else float(herm(self.x, self.y, self.m, t))

# ---------- 待机姿势（游戏里 Idle t=0 经过 Compute3 之后），攻击从这里出发、回到这里 ----------
IDLE = dict(px=0., py=0., tw=0., lean=0., shx=0., head=2.1, A=-8.9, E=16., th=68., Af=-7.1, Ef=16., L=4., K=8., F=4.)
def fk_arm(shoulder, Aw, E):
    el = shoulder + rot(SV1, Aw); wr = el + rot(SV2, Aw + E); return el, wr
def torso_pts(px, py, lean, shx, far=False, ratio=.8):
    hipw = np.array([px, py]) + HIP
    return hipw, hipw + rot((SHF if far else SH) + np.array([(-ratio * shx) if far else shx, 0.]), -lean)
def idle_hands():
    _, shN = torso_pts(0, 0, 0, 0); _, shF = torso_pts(0, 0, 0, 0, True)
    _, wN = fk_arm(shN, IDLE['A'], IDLE['E']); _, wF = fk_arm(shF, IDLE['Af'], IDLE['Ef'])
    dN = wN - shN; dF = wF - shF
    return (ang(dN), float(np.linalg.norm(dN))), (ang(dF), float(np.linalg.norm(dF)))
def idle_ankles():
    out = []
    for off in (LEGN, LEGF):
        hj = HIP + off; kn = hj + rot(LV1, IDLE['L']); an = kn + rot(LV2, IDLE['L'] - LR2 - IDLE['K'])
        out.append(an)
    return out
(IHA, IHR), (IFA, IFR) = idle_hands(); IANK = idle_ankles()

GAMMA, WMIN, WMAX = 80., -35., 25.   # 匕首 / 重武器 / 长杆的锤握
_sole = FOOT_SIL[FOOT_SIL[:, 1] > SOLE - .6]
BALL = np.array([float(np.percentile(_sole[:, 0], 80)), SOLE]); HEEL = np.array([float(np.percentile(_sole[:, 0], 8)), SOLE])

class Attack:
    """keys：通道 → [(秒, 值)]。通道：
       px py（骨盆位移）tw（髋转：近侧胯往前为正）lean（上身前倾）shx（近侧肩往前，远侧肩自动反向 0.8 倍）head（头相对世界的前倾）
       ha hr（近侧手腕：以肩为圆心的世界角、离肩多远）th（握持角：武器相对前臂，45 小指侧压到底 … 105 拇指侧翘到底）
       fa fr（远侧手腕：同上）
       nx nl np / fx fl fp（近 / 远侧脚：脚踝 x、抬起多高、鞋的角度（鞋尖抬起为正））"""
    def __init__(self, name, dur, hit, keys, trail=(), note='', kind='swing', weapon='dagger', idle=None):
        self.name, self.dur, self.hit, self.note, self.trail, self.kind, self.weapon = name, dur, hit, note, trail, kind, weapon
        self.geo = weapon_geo(weapon); self.idle = idle or {}
        base = dict(px=0, py=0, tw=0, lean=0, shx=0, head=IDLE['head'], ha=IHA, hr=IHR, th=IDLE['th'], fa=IFA, fr=IFR,
                    nx=float(IANK[0][0]), nl=0, np=0, fx=float(IANK[1][0]), fl=0, fp=0)
        base.update(self.idle)   # 这类武器自己的待机姿势（锤扛着、长杆端着）
        if self.geo['grip2'] is not None: base['g2'] = float(np.linalg.norm(self.geo['grip2'])) * (1 if self.geo['grip2'] @ self.geo['tip'] > 0 else -1)   # 另一只手在柄上的位置（沿柄，往武器头为正）
        self.c = {}
        for k, v0 in base.items():
            ks = keys.get(k, [])
            if not any(abs(t) < 1e-9 for t, _ in ks): ks = [(0., v0)] + ks
            if not any(abs(t - dur) < 1e-9 for t, _ in ks): ks = ks + [(dur, v0)]
            self.c[k] = Curve(ks)
    def raw(self, t): return {k: c(t) for k, c in self.c.items()}

def solve(a, t):
    r = a.raw(t); out = dict(t=t)
    px, py, tw, lean, shx = r['px'], r['py'], r['tw'], r['lean'], r['shx']
    rat = .8 if a.geo['grip2'] is None else .3   # 双手武器：两个肩膀转得少（两只手都要够得着柄）
    hipw, shN = torso_pts(px, py, lean, shx); _, shF = torso_pts(px, py, lean, shx, True, rat)
    # 腿：脚踝目标 → IK（膝盖朝前）
    legs = []
    for s, (xk, lk, pk, off, sgn) in enumerate((('nx', 'nl', 'np', LEGN, 1), ('fx', 'fl', 'fp', LEGF, -1))):
        pitch = r[pk]; piv = BALL if pitch < 0 else HEEL                     # 抬脚跟绕前掌转，鞋尖抬起绕脚跟转
        ank = np.array([r[xk] + piv[0], -r[lk] - 0.]) - rot(piv, pitch); ank[1] += piv[1] - piv[1]
        lo = max(float(q[1]) for q in (ank + np.array([rot(v, pitch) for v in FOOT_SIL])))   # 最低点贴地（抬脚时离地 lift）
        ank[1] -= lo + r[lk]
        hj = np.array([px, py]) + HIP + off + np.array([sgn * tw, 0.])
        d1, d2 = ik2(ank, hj, L1, L2, +1)
        L = d1; K = d1 - d2; F = pitch - L + LR2 + K
        reach = float(np.linalg.norm(ank - hj)) / (L1 + L2)
        legs.append(dict(L=L, K=K, F=F, ank=ank, hip=hj, knee=hj + rot(LV1, L), reach=reach))
    # 近侧手臂：手腕目标 → IK（手肘往后下弯，前臂相对上臂往前转 = 屈）
    wt = shN + rot(np.array([0., r['hr']]), r['ha'])
    d1, d2 = ik2(wt, shN, A1, A2, -1)
    Aw = d1 - SR1; E = d2 - SR2 - Aw
    th = r['th']; om = min(max(th - GAMMA, WMIN), WMAX); w = Aw + E + th
    el, wr = fk_arm(shN, Aw, E)
    G = a.geo
    grip = wr + rot(HAND_GRIP, Aw + E + om); piv = grip - rot(G['grip'], w); tip = piv + rot(G['tip'], w); butt = grip + rot(np.array([0., -6.4]), w)
    # 远侧手臂：双手武器 → 拳头握在柄上（握孔对准柄上第二个握点，拳头和柄的角度同近侧手），IK 求手臂；单手 → 按动作表
    if G['grip2'] is not None:
        ax = G['tip'] / float(np.linalg.norm(G['tip'])); g2 = piv + rot(ax * r['g2'], w); hfr = w - G['gamma']; ft = g2 - rot(HAND_GRIP_F, hfr)
    else:
        g2 = None; ft = shF + rot(np.array([0., r['fr']]), r['fa'])
    f1, f2 = ik2(ft, shF, A1, A2, -1); Afw = f1 - SR1; Ef = f2 - SR2 - Afw
    elF, wrF = fk_arm(shF, Afw, Ef)
    out['g2'] = g2; out['reachF'] = float(np.linalg.norm(ft - shF)) / (A1 + A2)
    out.update(r=r, legs=legs, hip=hipw, shN=shN, shF=shF, el=el, wr=wr, grip=grip, tip=tip, butt=butt, elF=elF, wrF=wrF,
               Aw=Aw, E=E, w=w, th=th, om=om, Afw=Afw, Ef=Ef, reachN=float(np.linalg.norm(wt - shN)) / (A1 + A2))
    # 游戏的通道（上身坐标系：A、W、Af 都要加 lean；头 = 世界前倾 - lean）
    out['game'] = dict(dx=px, dy=py, lean=lean, head=r['head'] - lean, shx=shx, shxF=-rat * shx, tw=tw, g2d=r.get('g2', 0.),
                       A=Aw + lean, E=E, W=w + lean, Af=Afw + lean, Ef=Ef,
                       L0=legs[0]['L'], K0=legs[0]['K'], F0=legs[0]['F'], L1=legs[1]['L'], K1=legs[1]['K'], F1=legs[1]['F'])
    return out

def idle_from(A, E, W, lean=0.):
    """游戏里某类武器的待机（上身坐标系的 A、E、W）→ 动作表的起点（手腕角、距离、握持角）。"""
    _, shN = torso_pts(0, 0, lean, 0); _, wN = fk_arm(shN, A - lean, E); d = wN - shN
    return dict(ha=ang(d), hr=float(np.linalg.norm(d)), th=W - A - E)
IDLE_HEAVY = idle_from(-12.9, 20., 59.1)     # 重武器待机（rig 3 新改）：垂手低握，锤头在身前低处（不再竖在脸前）
IDLE_POLE = idle_from(-18., 75., 107.)       # 长杆待机（rig 3 新改）：后手在腰边，杆子斜指前上，前手握在杆上
