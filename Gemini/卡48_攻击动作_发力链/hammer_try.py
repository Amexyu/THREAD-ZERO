import sys; sys.path.insert(0, '.')
from solve import *
import attacks, rig, hammer_tune as HT
import numpy as np, re, ast
src = open('attacks.py', encoding='utf-8').read()
def hammer(over):
    base = [x for x in attacks.ALL if x.name == 'hammer_smash'][0]
    # 从源码里取原 keys
    m = re.search(r"Attack\('hammer_smash'.*?keys=dict\((.*?)\n \), trail=\(\((.*?)\),\)\)", src, re.S)
    body = m.group(1)
    keys = {}
    for line in [l for l in body.strip().splitlines() if l.strip() and not l.strip().startswith('#')]:
        line = re.sub(r'#.*$', '', line).strip(); k, v = line.split('=', 1); keys[k] = ast.literal_eval(v.strip().rstrip(','))
    keys.update(over)
    return Attack('hammer_smash', base.dur, base.hit, keys, trail=base.trail, weapon='hammer', idle=IDLE_HEAVY, note=base.note)
def show(a, t0=0, t1=.68, step=.01, every=1):
    for i, t in enumerate(np.arange(t0, t1 + 1e-9, step)):
        s = solve(a, t); fh = HT.face_hit(s); L = HT.low(s)
        hip = s['hip']; lean = s['r']['lean']; hp = hip + rot(rig.HEAD, -lean); d = rot(s['wr'] - hp, lean); de = rot((s['wr'] + s['el']) / 2 - hp, lean)
        flag = ('挡脸(腕)' if fh[0] else '') + ('挡脸(臂)' if fh[1] else '') + ('钻地 %.1f' % L if L > .5 else '')
        if i % every == 0 or flag:
            print('t %.3f w %5.0f th %5.0f om %4.0f E %4.0f 腕-头 (%6.1f,%6.1f) 臂中-头 (%6.1f,%6.1f) 最低角 %6.1f %s' % (t, s['w'], s['th'], s['om'], s['E'], d[0], d[1], de[0], de[1], L, flag))
