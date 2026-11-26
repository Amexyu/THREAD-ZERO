# 着地期参数搜索：让 触地胯高 ≈ 蹬离胯高、蹬离时身体还在往上、下沉 2-3.5、角度都在真人范围里
import sys, itertools, math, numpy as np
sys.argv = ['x', '../卡42_主角标准站立图/部件A3', 'out']
import run_ref as R
def setup(th_ic, k_ic, k_dn, k_pass, k_late, k_to, st, f_to):
    R.ST = st; R.TH_IC = th_ic; R.F_TO = f_to
    R.K_KEYS = [(0., k_ic), (.075, k_dn), (.16, k_pass), (st - .08, k_late), (st, k_to)]
    R._kx = np.array([k[0] for k in R.K_KEYS]); R._ky = np.array([k[1] for k in R.K_KEYS]); R._km = R.pchip_slopes(R._kx, R._ky); R._km[0] = 0.
    R.G0 = R.find_G0(); R.H_TD = R.stance_solve(0, 0.)['H']
def evaluate():
    ws = np.linspace(0, R.ST - 1e-4, 60); rs = [R.stance_solve(0, w) for w in ws]
    H = np.array([r['H'] for r in rs]); A = np.array([r['A'] for r in rs])
    to = rs[-1]; slope = (H[-1] - H[-3]) / (ws[-1] - ws[-3])
    return dict(H_TD=H[0], H_min=H.min(), w_min=ws[np.argmin(H)], H_TO=H[-1], slope=slope, th_TO=to['th'], K_TO=to['K'], A_TO=to['A'], Amax=A.max(), Hmax_st=H.max(), w_hmax=ws[np.argmax(H)])
if __name__ == "__main__":
    best = []
    for th_ic, k_ic, k_dn, k_to, st, f_to in itertools.product([20, 23, 26], [12, 16, 20], [48, 54], [16, 20, 24], [.33, .35, .37], [-26, -32, -38]):
        k_pass = (k_dn + k_to) / 2 + 4; k_late = k_to + 4
        setup(th_ic, k_ic, k_dn, k_pass, k_late, k_to, st, f_to); e = evaluate()
        # 目标：蹬离胯高比触地高 0~0.8，蹬离时还在升，着地期最高点就在蹬离，下沉 2~3.5，蹬离大腿 -20~-28
        cost = abs(e['H_TO'] - e['H_TD'] - .4) * 3 + max(0, -e['slope']) * .2 + max(0, e['Hmax_st'] - e['H_TO']) * 4
        cost += max(0, 2.0 - (e['H_TD'] - e['H_min'])) * 2 + max(0, (e['H_TD'] - e['H_min']) - 3.5) * 2
        cost += max(0, e['th_TO'] + 20) * .5 + max(0, -28 - e['th_TO']) * .5 + max(0, e['A_TO'] - 18) * .3
        best.append((cost, (th_ic, k_ic, k_dn, k_to, st, f_to), e))
    best.sort(key=lambda x: x[0])
    for c, p, e in best[:10]:
        print('%.2f' % c, p, ' '.join('%s=%.2f' % (k, v) for k, v in e.items()))
