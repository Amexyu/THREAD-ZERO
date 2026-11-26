# 卡48：把攻击动作导出成游戏的 attack3.json（每个动作按时长等分采样，游戏里线性插值；角度都是 Build3 的约定）
import json, math, numpy as np
from solve import *
import attacks
N = 100
def pack_sway(a, ts, S):   # 背包：跟着上身的水平加速度晚一拍晃（弹簧 + 阻尼）
    top = np.array([s['hip'][0] + math.sin(math.radians(s['r']['lean'])) * 40 for s in S]); dt = ts[1] - ts[0]
    acc = np.gradient(np.gradient(top, dt), dt); w0 = 2 * math.pi * 3.2; z = .32; th = 0.; v = 0.; out = []
    for x in acc:
        f = -w0 * w0 * th - 2 * z * w0 * v - .0016 * x; v += f * dt; th += v * dt; out.append(th)
    return np.clip(np.array(out), -9, 9)
def export(path):
    res = []
    for a in attacks.ALL:
        fine = np.linspace(0, a.dur, 600); Sf = [solve(a, t) for t in fine]; pk = pack_sway(a, fine, Sf)
        ts = np.linspace(0, a.dur, N); S = [solve(a, t) for t in ts]
        e = dict(name=a.name, n=N, dur=a.dur, hit=round(a.hit / a.dur, 4))
        keys = ['dx', 'dy', 'lean', 'head', 'shx', 'shxF', 'tw', 'A', 'E', 'W', 'Af', 'Ef', 'L0', 'K0', 'F0', 'L1', 'K1', 'F1', 'g2d']
        for k in keys:
            v = np.array([float(s['game'][k]) for s in S])
            if k in ('A', 'W', 'Af', 'L0', 'L1'): v = np.degrees(np.unwrap(np.radians(v)))   # 角度连续（转过一圈的不在 ±180 处跳）
            e[k] = [round(float(x), 3) for x in v]
        e['pack'] = [round(float(np.interp(t, fine, pk)), 3) for t in ts]
        e['fx'] = [1 if any(t0 <= t <= t1 for t0, t1 in a.trail) else 0 for t in ts]
        res.append(e)
    json.dump(dict(attacks=res), open(path, 'w'), separators=(',', ':'))
    return [r['name'] for r in res]
if __name__ == '__main__':
    import sys
    print(export(sys.argv[1]))
