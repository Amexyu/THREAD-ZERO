# 卡41：8 件上色工具（Gemini 部件图，漆的部分是纯绿 #00FF00）→ 游戏武器部件。
# 每件输出 weapon.png（漆的地方变成白）+ paint.png（漆的形状，白色，游戏里按漆色染）+ part.json。
# 点位都是原图像素（部件自己的坐标）：grip = 手握的点（骨骼的转轴），tip = 干活的一头（枪口 / 刷尖 / 锤面）。
import os, sys, json, numpy as np
from PIL import Image
CUT = '切图'; OUT = sys.argv[1]
allim = np.asarray(Image.open(os.path.join(CUT, '_all.png'))); lab = np.load(os.path.join(CUT, '_lab.npy'))
comps = {c['i']: c for c in json.load(open(os.path.join(CUT, '_comps.json')))}
SCALE = 5.0
TOOLS = [  # id, 部件编号, 动作族, 目标高度（单位）, grip, tip, 切掉 y 以下（-1 不切）
    ('t1', 2, 'dagger', 72, (165, 210), (165, 880), -1),       # 大排笔
    ('t2', 5, 'gun', 60, (150, 260), (150, 540), -1),          # 喷漆枪（喷笔）
    ('t3', 4, 'shotgun', 54, (205, 22), (95, 335), 395),       # 油漆桶（下面流出来的那滩切掉）
    ('t4', 7, 'cannon', 64, (130, 105), (50, 360), -1),        # 颜料气球炮
    ('t5', 3, 'heavy', 94, (160, 110), (160, 965), -1),        # 印章锤
    ('t6', 8, 'chain', 46, (128, 8), (128, 245), -1),          # 颜料溜溜球（做吊钩用）
    ('t7', 6, 'coil', 72, (205, 120), (75, 742), -1),          # 高压水管喷枪
    ('t8', 1, 'pole', 112, (213, 110), (220, 1085), -1),       # 长柄滚筒
]
for tid, ci, cls, hu, grip, tip, cut in TOOLS:
    c = comps[ci]; x0, y0 = c['x'], c['y']
    a = allim[y0:y0 + c['h'], x0:x0 + c['w']].copy().astype(np.float32)
    m = lab[y0:y0 + c['h'], x0:x0 + c['w']] == ci
    a[..., 3] = np.where(m, a[..., 3], 0)
    if cut > 0: a[cut:, :, 3] = 0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    green = np.clip((g - np.maximum(r, b) - 30) / 110, 0, 1)
    lum = .3 * r + .59 * g + .11 * b
    base = a.copy()
    for k in range(3): base[..., k] = a[..., k] * (1 - green) + 255 * green   # 漆的地方先变白
    paint = np.zeros_like(a); paint[..., :3] = 255; paint[..., 3] = green * a[..., 3]
    k = hu * SCALE / c['h']
    W, H = round(c['w'] * k), round(c['h'] * k)
    d = os.path.join(OUT, tid); os.makedirs(d, exist_ok=True)
    Image.fromarray(base.astype(np.uint8)).resize((W, H), Image.LANCZOS).save(os.path.join(d, 'weapon.png'))
    Image.fromarray(paint.astype(np.uint8)).resize((W, H), Image.LANCZOS).save(os.path.join(d, 'paint.png'))
    U = lambda p: [round(p[0] * k / SCALE, 2), round(p[1] * k / SCALE, 2)]
    pj = dict(scale=SCALE, weapon_class=cls, weapon=dict(file='weapon.png', pivot=U(grip), tip=U(tip)), paint=dict(file='paint.png', pivot=U(grip)))
    if cls == 'chain':   # 溜溜球：手里那段「硬武器」不画，只用它的尖端带着吊钩走；吊钩就是溜溜球本身
        pj['weapon']['tip'] = [round(grip[0] * k / SCALE, 2), 72.0]
        pj['hook'] = dict(file='weapon.png', pivot=U(grip)); pj['hook_paint'] = dict(file='paint.png', pivot=U(grip)); pj['chain'] = 'cable'
    json.dump(pj, open(os.path.join(d, 'part.json'), 'w'), ensure_ascii=False, indent=1)
    print(tid, cls, (W, H), pj['weapon'])
