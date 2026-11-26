# 卡48：火柴人预览（看姿势、弧线、刀尖轨迹；真正的样子看游戏里的姿势表）
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFont
from solve import *
import attacks
F = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', s, index=2)
PX = 2.2; CW, CH = 420, 440
def draw(a, t, trail=True):
    im = Image.new('RGB', (CW, CH), (246, 244, 238)); d = ImageDraw.Draw(im)
    ox, oy = CW * .36, CH - 40
    P = lambda v: (ox + v[0] * PX, oy + v[1] * PX)
    d.line([(0, oy), (CW, oy)], fill=(120, 120, 120), width=2)
    if trail:   # 刀尖轨迹（整段，命中点红色）
        pts = [P(solve(a, u)['tip']) for u in np.linspace(0, a.dur, 80)]
        d.line(pts, fill=(200, 200, 215), width=2)
        hp = P(solve(a, a.hit)['tip']); d.ellipse([hp[0] - 5, hp[1] - 5, hp[0] + 5, hp[1] + 5], outline=(230, 40, 40), width=2)
    s = solve(a, t)
    far = (90, 110, 160); near = (30, 30, 30)
    # 远侧：腿、手臂
    L = s['legs'][1]; d.line([P(L['hip']), P(L['knee']), P(L['ank'])], fill=far, width=6)
    d.line([P(s['shF']), P(s['elF']), P(s['wrF'])], fill=far, width=6); d.ellipse([P(s['wrF'])[0] - 6, P(s['wrF'])[1] - 6, P(s['wrF'])[0] + 6, P(s['wrF'])[1] + 6], fill=far)
    # 身体：胯 → 肩，头
    hip = s['hip']; nk = s['shN']
    d.line([P(hip), P((s['shN'] + s['shF']) / 2)], fill=(200, 150, 40), width=14)
    hc = hip + rot(HEAD + np.array([0, -14.]), -s['r']['lean']); r = 17 * PX; c = P(hc)
    d.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], outline=(80, 80, 80), width=3)
    # 近侧腿、手臂、刀
    L = s['legs'][0]; d.line([P(L['hip']), P(L['knee']), P(L['ank'])], fill=near, width=7)
    for L, col in ((s['legs'][1], far), (s['legs'][0], near)):   # 鞋：脚跟 → 脚踝 → 前掌 → 鞋尖
        pt = L['F'] + L['L'] - LR2 - L['K']; q = [L['ank'] + rot(v, pt) for v in (HEEL, np.array([0., 0.]), BALL, np.array([BALL[0] + 6, SOLE - 2]))]
        d.line([P(q[1]), P(q[0]), P(q[2]), P(q[3])], fill=col, width=4)
    d.line([P(s['butt'] if a.weapon == 'dagger' else s['grip'] - (s['tip'] - s['grip']) * .2), P(s['tip'])], fill=(120, 120, 130), width=5)
    if s.get('g2') is not None: q = P(s['g2']); d.ellipse([q[0] - 6, q[1] - 6, q[0] + 6, q[1] + 6], fill=(150, 170, 220), outline=near)
    d.line([P(s['shN']), P(s['el']), P(s['wr'])], fill=(230, 160, 30), width=7); g = P(s['grip']); d.ellipse([g[0] - 7, g[1] - 7, g[0] + 7, g[1] + 7], fill=(240, 200, 170), outline=near)
    d.text((8, 6), '%s  %.0fms' % (a.name, t * 1000), fill=(40, 40, 40), font=F(18))
    if abs(t - a.hit) < 1e-6: d.text((8, 30), '命中', fill=(220, 30, 30), font=F(20))
    return im
if __name__ == '__main__':
    out = sys.argv[1]; names = sys.argv[2:] or [a.name for a in attacks.ALL]
    for a in attacks.ALL:
        if a.name not in names: continue
        sc_ = a.dur / .33; ts = [round(x * sc_, 3) for x in (0, .03, .055, .07, .085, .10, .115, .13)] + [a.hit] + [round(x * sc_, 3) for x in (.16, .19, .23, .27, .33)]
        st = Image.new('RGB', (CW * 7, CH * 2), 'white')
        for i, t in enumerate(ts): st.paste(draw(a, t), ((i % 7) * CW, (i // 7) * CH))
        st.save('%s/stick_%s.jpg' % (out, a.name), quality=88)
    print('ok')
