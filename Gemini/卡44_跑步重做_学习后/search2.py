import sys, itertools, numpy as np
sys.argv = ['x', '../卡42_主角标准站立图/部件A3', 'out']
import run_ref as R
from search import setup, evaluate
rows = []
for f_to, pw, wp, k_to, st in itertools.product([-30, -34, -38, -42], [1.0, 1.6, 2.2, 3.0], [.10, .14, .18], [20, 24, 28], [.33, .35]):
    R.W_PUSH, R.PUSH_POW = wp, pw
    setup(20, 16, 54, (54 + k_to) / 2 + 4, k_to + 4, k_to, st, f_to); e = evaluate()
    d_to = e['H_TO'] - e['H_TD']
    cost = abs(d_to - .5) * 2 + max(0, 8 - e['slope']) * .15 + max(0, e['Hmax_st'] - e['H_TO']) * 4 + max(0, -28 - e['th_TO']) + max(0, e['th_TO'] + 19) + max(0, e['A_TO'] - 15) * .3
    rows.append((cost, (f_to, pw, wp, k_to, st), e))
rows.sort(key=lambda x: x[0])
for c, p, e in rows[:8]: print('%.2f' % c, p, ' '.join('%s=%.2f' % (k, v) for k, v in e.items()))
