# 锤：调「举锤时手别挡脸」「砸地时锤头不钻地」用的探查脚本
import sys, copy; sys.path.insert(0, '.')
from solve import *
import attacks, rig
import numpy as np
C = [np.array(c) for c in ((15.9, 8.75), (-15.9, 8.75), (15.9, -8.75), (-15.9, -8.75))]   # 锤头方块的四个角（相对锤头中心，武器坐标）
def low(s): return max((s['tip'] + rot(c, s['w']))[1] for c in C)
def face_hit(s):   # 会不会挡脸（相对头点，上身坐标）：手腕 / 前臂中点进了脸的范围（x 2..20，y -27..5），或者拳头（半径约 6）进了（x -3..20）
    hip = s['hip']; lean = s['r']['lean']; hp = hip + rot(rig.HEAD, -lean)
    def inside(p, xmin): d = rot(p - hp, lean); return bool(xmin <= d[0] <= 20 and -27 <= d[1] <= 5)
    return [inside(s['wr'], 2), inside((s['wr'] + s['el']) / 2, 2), inside(s['grip'], -3)]
def report(a, ts=None):
    ts = ts or [i / 200 * a.dur for i in range(201)]
    for t in ts:
        s = solve(a, t); fh = face_hit(s); L = low(s)
        flag = ('挡脸(腕)' if fh[0] else '') + ('挡脸(臂)' if fh[1] else '') + ('钻地 %.1f' % L if L > .5 else '')
        yield t, s, L, flag
if __name__ == '__main__':
    a = [x for x in attacks.ALL if x.name == 'hammer_smash'][0]
    for t, s, L, flag in report(a):
        if flag or abs(t * 1000 % 20) < 3.4: print('t %.3f w %5.0f th %5.0f 腕 (%6.1f,%6.1f) 最低角 %6.1f %s' % (t, s['w'], s['th'], s['wr'][0], s['wr'][1], L, flag))
