# 卡48：检查攻击动作——发力顺序（各段角速度 / 速度的峰值时间要从近端到远端依次往后）、握持角范围、关节范围、脚有没有滑
import sys, math, numpy as np
from solve import *
import attacks
def series(a, dt=1/600):
    ts = np.arange(0, a.dur + 1e-9, dt); S = [solve(a, t) for t in ts]; return ts, S
def chain(a):
    ts, S = series(a)
    g = lambda f: np.array([f(s) for s in S])
    ga = lambda f: np.degrees(np.unwrap(np.radians(g(f))))   # 角度：去掉 ±180 的跳变
    tip = g(lambda s: s['tip']); hand = g(lambda s: s['wr'])
    if a.kind == 'thrust':   # 刺：看直线速度——骨盆 → 肩膀 → 上臂往前抬 → 手肘伸直 → 手 → 刀尖
        ch = dict(骨盆前移=g(lambda s: s['r']['px']), 髋转=g(lambda s: s['r']['tw']), 上身前压=g(lambda s: s['r']['lean']), 肩前送=g(lambda s: s['shN'][0]),
                  上臂=ga(lambda s: s['Aw']), 前臂=g(lambda s: -s['E']), 武器=g(lambda s: s['wr'][0]))
    else:
        ch = dict(骨盆前移=g(lambda s: s['r']['px']), 髋转=g(lambda s: s['r']['tw']), 上身前压=g(lambda s: s['r']['lean']), 肩前送=g(lambda s: s['r']['shx']),
                  上臂=ga(lambda s: s['Aw']), 前臂=ga(lambda s: s['Aw'] + s['E']), 武器=ga(lambda s: s['w']))
    dt = ts[1] - ts[0]; w0 = a.hit - (.055 if a.dur < .5 else .11); w1 = a.hit + .02
    sel = (ts >= w0) & (ts <= w1); res = []
    for k, v in ch.items():
        sp = np.abs(np.gradient(v, dt)); i = int(np.argmax(np.where(sel, sp, 0))); res.append((k, ts[i], sp[i]))
    tsp = np.linalg.norm(np.gradient(tip, dt, axis=0), axis=1); hsp = np.linalg.norm(np.gradient(hand, dt, axis=0), axis=1)
    it = int(np.argmax(np.where(sel, tsp, 0))); ih = int(np.argmax(np.where(sel, hsp, 0)))
    return res, (ts[ih], hsp[ih]), (ts[it], tsp[it]), ts, S
def report(a):
    res, (th_, hv), (tt, tv), ts, S = chain(a)
    print('== %s（%s）命中 %.3f 秒' % (a.name, a.note[:16], a.hit))
    print('   发力顺序（出手阶段各段最快的时刻%s）：' % ('；刺：肩=肩膀前移，前臂=手肘伸直，武器=手腕前移' if a.kind == 'thrust' else '') + ' → '.join('%s %.0fms' % (k, t * 1000) for k, t, _ in res))
    T = {k: t for k, t, _ in res}
    groups = [min(T['骨盆前移'], T['髋转']), min(T['上身前压'], T['肩前送']), T['上臂'], T['前臂'], T['武器']]   # 骨盆 → 上身 → 上臂 → 前臂 → 武器
    ok = all(groups[i] <= groups[i + 1] + .010 for i in range(len(groups) - 1))   # 容差 10ms（不到一帧）
    tsp = None
    print('   手腕最快 %.0fms（%.0f 单位/秒），刀尖最快 %.0fms（%.0f 单位/秒） | 顺序%s' % (th_ * 1000, hv, tt * 1000, tv, '对' if ok else '不对！'))
    bad = []
    for s in S:
        t = s['t']
        if not (44.5 <= s['th'] <= 105.5): bad.append((t, '握持角 %.0f' % s['th']))
        if not (-12 <= s['E'] <= 150): bad.append((t, '手肘 %.0f' % s['E']))
        for j, L in enumerate(s['legs']):
            if not (-2 <= L['K'] <= 125): bad.append((t, '%s膝 %.0f' % ('近远'[j], L['K'])))
            if L['reach'] > .9999: bad.append((t, '%s腿够不着 %.4f' % ('近远'[j], L['reach'])))
        if s['reachN'] > .9999: bad.append((t, '手够不着'))
        if s.get('reachF', 0) > .9999: bad.append((t, '远侧手够不着柄'))
        if a.weapon != 'dagger':
            bx = a.geo.get('box')
            lowp = max((s['tip'] + rot(np.array(c), s['w']))[1] for c in ((bx[0], bx[1]), (-bx[0], bx[1]), (bx[0], -bx[1]), (-bx[0], -bx[1]))) if bx else s['tip'][1]
            if lowp > .5: bad.append((t, '武器头钻进地里 %.1f' % lowp))
    from collections import OrderedDict
    g = OrderedDict()
    for t, msg in bad: g.setdefault(msg.split(' ')[0], []).append(t)
    print('   不合格：' + ('无' if not g else '；'.join('%s %.3f..%.3f' % (k, min(v), max(v)) for k, v in g.items())))
    for t in [round(x * a.dur / .33, 3) for x in (0, .06, .1)] + [a.hit] + [round(x * a.dur / .33, 3) for x in (.19, .26)]:
        s = solve(a, t); gm = s['game']
        print('   t=%.3f 上臂 %5.0f 手肘 %4.0f 握持 %4.0f 武器 %5.0f 头 (%5.1f,%5.1f) | 远手 %5.0f/%4.0f | 近腿 L %5.1f K %5.1f | 远腿 L %5.1f K %5.1f' % (
            t, s['Aw'], s['E'], s['th'], s['w'], s['tip'][0], s['tip'][1], s['Afw'], s['Ef'], gm['L0'], gm['K0'], gm['L1'], gm['K1']))
if __name__ == '__main__':
    for a in attacks.ALL: report(a)
