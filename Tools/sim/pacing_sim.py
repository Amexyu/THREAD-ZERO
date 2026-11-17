"""零织协议 · 节奏模拟（v0.16）
用游戏里的公式粗算：第一章每关时长、整章时长，回声层 1 天 / 1 周 / 1 月能到第几层。
简化：单目标 DPS × 群体系数；怪按「同时贴身 3 只 + 远程」打人；每层/每关结束按掉落换装、升级、训练。
用法：python3 pacing_sim.py [敌人血量倍率] [回声血量成长] [回声攻击成长]
"""
import random, math, sys
random.seed(7)
HPMUL = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
EG_H = float(sys.argv[2]) if len(sys.argv) > 2 else 1.06
EG_A = float(sys.argv[3]) if len(sys.argv) > 3 else 1.045
TRAINCAP = int(sys.argv[4]) if len(sys.argv) > 4 else 20
ECHOLIC = sys.argv[5] if len(sys.argv) > 5 else 'best'
EBASE = float(sys.argv[6]) if len(sys.argv) > 6 else 1.1
UPC = float(sys.argv[7]) if len(sys.argv) > 7 else 1
# ---- 游戏数值（与 Balance / Chapters / GameModel 一致）----
BASE = dict(hp=140, atk=16, df=4, crit=.05)
QMULT = [1, 1.04, 1.08, 1.12, 1.16, 1.20]; AFFIXN = [0, 1, 2, 3, 4, 5]
AFFIX = [12, 3, 2, .04, .025, 2]   # hp atk def haste crit luck
TPL = {0: (0, 7, 0, 0), 1: (10, 0, 2, 0), 2: (24, 0, 3, 0), 3: (12, 0, 2, .02)}   # 每部位代表性模板 hp atk def haste
QW = [50, 28, 15, 6, 1]
ROLES = [(.45, .5, 1.3), (1.4, 1.0, 2.0), (.7, .6, 1.9)]
CH1 = [[(2,0,0,0),(3,0,0,0),(4,0,0,0)],[(3,0,0,0),(3,1,0,0),(3,2,0,0)],[(3,1,0,0),(4,1,0,0),(2,0,0,1)],
       [(3,1,0,0),(3,1,1,0),(3,2,1,0)],[(3,1,1,0),(3,2,1,0),(8,0,0,0)],[(3,1,1,0),(3,2,1,0),(3,0,0,2)],'boss']
DROPLV = [(1,3),(2,4),(4,6),(5,7),(7,9),(8,11),(12,15)]
DROPCH = [.25, .5, .4]
def lvf(l): return 1 + .08 * (min(l, 100) - 1) + .04 * max(0, l - 100)   # 100 级以后每级收益减半
def dmg(a, d): return max(1, a * 100 / (100 + max(0, d)))
class Item:
    def __init__(s, slot, lvl, q):
        s.slot, s.lvl, s.q = slot, lvl, q
        s.aff = [random.randrange(6) for _ in range(AFFIXN[q])]; s.roll = [random.uniform(.85, 1.15) for _ in s.aff]
    def stats(s):
        hp, atk, df, ha = TPL[s.slot]; f = QMULT[s.q] * lvf(s.lvl)
        st = [hp * f, atk * f, df * f, ha, 0, 0]
        for a, r in zip(s.aff, s.roll): st[a] += AFFIX[a] * r * (1 + .06 * (s.lvl - 1))
        return st
    def score(s): st = s.stats(); return st[0] * .25 + st[1] * 3 + st[2] * 1.5 + st[3] * 60 + st[4] * 80
class Player:
    def __init__(s):
        s.gear = {k: Item(k, 1, 0) for k in range(4)}; s.train = 0; s.scrap = 0; s.parts = 0; s.pity = 0; s.license = 5; s.implant = 0
    def stats(s):
        t = [BASE['hp'] + 8 * s.train, BASE['atk'] + 1.5 * s.train, BASE['df'], 0, BASE['crit'], 0]
        for it in s.gear.values():
            for i, v in enumerate(it.stats()): t[i] += v
        t[3] = min(2, t[3] + .15 * s.implant); t[4] = min(.75, t[4]); return t
    def drop(s, lvl, minq=0):
        r = random.uniform(0, sum(QW)); q = 4
        for i, w in enumerate(QW):
            r -= w
            if r < 0: q = i; break
        q = max(q, minq)
        if s.pity >= 12: q = max(q, 2)
        s.pity = 0 if q >= 2 else s.pity + 1
        it = Item(random.randrange(4), lvl, q)
        cur = s.gear[it.slot]
        if it.score() > cur.score(): s.parts += (2 + cur.q * 3) * cur.lvl; s.gear[it.slot] = it
        else: s.parts += (2 + it.q * 3) * it.lvl
    def spend(s):
        while s.train < TRAINCAP and s.scrap >= 20 + s.train * 15: s.scrap -= 20 + s.train * 15; s.train += 1
        for _ in range(40):   # 零件升级最低的那件
            it = min(s.gear.values(), key=lambda x: x.lvl)
            cost = round((6 + it.lvl * 2) * (1 + .4 * it.q)) * 4 * (UPC if it.lvl >= 30 else 1)   # parts 分四种，这里合并计 ×4
            if it.lvl >= s.license or s.parts < cost: break
            s.parts -= cost; it.lvl += 1
def fight(p, waves, hp_of, atk_of, df, boss=None):
    """返回 (是否通过, 用时秒)。"""
    st = p.stats(); hp = st[0]; aps = 1 + st[3]; crit = st[4]
    dps = dmg(st[1], df) * aps * (1 + crit * .75) * 1.35   # 1.35：范围 / 技能 / 模块的平均加成
    t = 0
    for w, (sw, br, ra, el) in enumerate(waves, 1):
        hps = [ROLES[0][0]] * sw + [ROLES[1][0]] * br + [ROLES[2][0]] * ra + [ROLES[1][0] * 2.6] * el
        atks = [ROLES[0][1] / ROLES[0][2]] * sw + [ROLES[1][1] / ROLES[1][2]] * br + [ROLES[2][1] / ROLES[2][2]] * ra + [1.4 / 2] * el
        total = sum(hps) * hp_of(w); tw = total / dps + 3.5      # 3.5 秒：涌入、走位
        pressure = (sum(sorted(atks, reverse=True)[:3 + ra]) * .55) * dmg(atk_of(w), st[2])   # 一半时间满压力
        hp -= pressure * tw; t += tw
        if hp <= 0: return False, t
        hp = min(st[0], hp + st[0] * .04)
    if boss:
        bhp = hp_of(5) * 6; tb = bhp / dps + 3
        hp -= (1.4 / 1.6 + .35) * dmg(atk_of(5) * 1.4, st[2]) * tb   # 首领 + 召唤的小怪
        t += tb
        if hp <= 0: return False, t
    return True, t
def chapter(p):
    clock = 0; stage = 0; log = []
    while stage < 7 and clock < 6 * 3600:
        waves = CH1[stage]; boss = waves == 'boss'
        hp_of = lambda w: (50 + 10 * w) * (1 + .16 * stage) * HPMUL; atk_of = lambda w: (4 + 1.2 * w) * (1 + .14 * stage)
        ok, t = fight(p, [] if boss else waves, hp_of, atk_of, 3 * stage, boss=boss)
        clock += t + 1.5
        lo, hi = DROPLV[stage]; n = 0
        if not boss:
            for (sw, br, ra, el) in waves:
                for role, cnt in ((0, sw), (1, br), (2, ra)):
                    for _ in range(cnt):
                        p.scrap += 1
                        if random.random() < DROPCH[role]: p.drop(random.randint(lo, hi)); n += 1
                for _ in range(el): p.drop(random.randint(lo, hi), 2); p.drop(random.randint(lo, hi)); n += 2
        else: [p.drop(hi, 2) for _ in range(3)]; p.scrap += 8
        p.spend()
        if ok:
            log.append((stage + 1, round(t / 60, 1), round(clock / 60, 1))); stage += 1; p.license = min(30, (stage + 1) * 5)
            if boss: p.implant = 1
        # 失败：退一关（这里简化为原地重刷，掉落照拿）
    return clock, log
def echo(p, start_clock, hours):
    clock = 0; floor = 1; best = 0; marks = {}
    if ECHOLIC == 'free': p.license = 300
    while clock < hours * 3600:
        kind = 'boss' if floor % 10 == 0 else 'elite' if floor % 5 == 0 else 'n'
        g_h = EBASE * EG_H ** (floor - 1); g_a = EBASE * EG_A ** (floor - 1)
        sw = min(9, 3 + floor // 10); br = min(3, 1 + floor // 20); ra = 0 if floor < 3 else min(3, 1 + floor // 25)
        el = min(3, 1 + floor // 30) if kind == 'elite' else 0
        waves = [] if kind == 'boss' else [(sw, br, ra, 0), (sw, br, ra, el)]
        hp_of = lambda w: (50 + 10 * w) * 1.96 * HPMUL * g_h; atk_of = lambda w: (4 + 1.2 * w) * 1.84 * g_a
        ok, t = fight(p, waves, hp_of, atk_of, 18 + floor * .5, boss=kind == 'boss')
        clock += t + 1.2
        lvl = 15 + floor // 2
        for (a, b, c, e) in waves:
            for role, cnt in ((0, a), (1, b), (2, c)):
                for _ in range(cnt):
                    p.scrap += 1
                    if random.random() < DROPCH[role]: p.drop(random.randint(lvl, lvl + 2))
            for _ in range(e): p.drop(lvl + 1, 2); p.drop(lvl + 1)
        if kind == 'boss': [p.drop(lvl + 2, 2) for _ in range(3)]
        p.spend()
        if ok:
            best = max(best, floor); p.license = 300 if ECHOLIC == 'free' else min(100, 30 + best // 2); floor += 1
        else: floor = max(1, floor - 3)
        for label, hrs in (('1天', 24), ('1周', 168), ('1月', 720)):
            if label not in marks and clock >= hrs * 3600 - 1: marks[label] = best
    return best, marks
p = Player()
c, log = chapter(p)
print(f'参数：敌人血量 ×{HPMUL}  回声每层血量 ×{EG_H}  攻击 ×{EG_A}')
print('第一章：', '  '.join(f'1-{s} 首通 {m} 分（累计 {tot} 分）' for s, m, tot in log))
print(f'第一章总时长（1×）：{c/60:.0f} 分钟')
best, marks = echo(p, c, 720)
print('回声层（1×，挂机不停）：', marks, '30 天最高层', best)
