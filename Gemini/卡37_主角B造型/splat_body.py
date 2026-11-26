# 卡37：主角 B 造型（方案①）的身体 —— 不重画，直接在初始部件（V13/Starter）的黑色布料上溅彩色喷漆，外形、线稿、骨骼锚点和游戏里完全一样。
# 写成正式部件：外套 c0（torso / arm / arm_far）、下装 l0（leg_near / leg_far），锚点照抄 Starter/parts.json。
# 用法：python3 splat_body.py [--preview 预览.png]
import os, sys, json, math, random, numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
A = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/')
S = json.load(open(A + 'Starter/parts.json'))
PAINT = [(255, 79, 163), (54, 214, 231), (255, 214, 10), (110, 231, 95), (255, 140, 40)]

def erode(m, it):
    m = m.copy()
    for _ in range(it):
        g = m.copy(); g[1:] &= m[:-1]; g[:-1] &= m[1:]; g[:, 1:] &= m[:, :-1]; g[:, :-1] &= m[:, 1:]; m = g
    return m

def splat(name, n, seed, scale=1.0, maxy=None):
    im = np.asarray(Image.open(A + 'Starter/' + name + '.png').convert('RGBA')).astype(np.float32)
    rgb, al = im[..., :3], im[..., 3]; H, W = al.shape
    L = rgb.mean(-1)
    cloth = erode((al > 200) & (L > 22) & (L < 95), 3)          # 黑色布料（去掉外轮廓一圈、浅色的护肩 / 鞋 / 吊牌 / 扣子）
    if maxy is not None: cloth[maxy:] = False                     # 腿：只在裤子上（鞋和鞋底不溅）
    ys, xs = np.nonzero(cloth)
    if len(ys) == 0: return Image.fromarray(im.astype(np.uint8), 'RGBA')
    rnd = random.Random(seed); paint = np.zeros((H, W), np.int16) - 1
    yy, xx = np.mgrid[0:H, 0:W]
    centers = []
    for k in range(n * 6):
        if len(centers) >= n: break
        i = rnd.randrange(len(ys)); cy, cx = ys[i], xs[i]
        if any((cy - a) ** 2 + (cx - b) ** 2 < (20 * scale) ** 2 for a, b in centers): continue
        centers.append((cy, cx)); col = len(centers) % len(PAINT) if k % 2 else rnd.randrange(len(PAINT))
        r = rnd.uniform(7, 12.5) * scale
        blob = np.zeros((H, W), bool)
        for j in range(rnd.randint(3, 5)):                       # 主块：几个重叠的圆
            a = rnd.uniform(0, 6.28); d = rnd.uniform(0, r * .55)
            rr = r * rnd.uniform(.55, .9)
            blob |= (yy - (cy + math.sin(a) * d)) ** 2 + (xx - (cx + math.cos(a) * d)) ** 2 < rr * rr
        for j in range(rnd.randint(3, 7)):                       # 溅出去的小点
            a = rnd.uniform(0, 6.28); d = r * rnd.uniform(1.25, 2.4); rr = rnd.uniform(1.4, 3.2) * scale
            blob |= (yy - (cy + math.sin(a) * d)) ** 2 + (xx - (cx + math.cos(a) * d)) ** 2 < rr * rr
        if rnd.random() < .45:                                   # 往下流一道
            dl = rnd.uniform(1.2, 2.2) * r; wv = max(1.6, r * .22)
            blob |= (np.abs(xx - cx - r * .15) < wv) & (yy > cy) & (yy < cy + dl)
            blob |= (yy - (cy + dl)) ** 2 + (xx - cx - r * .15) ** 2 < (wv * 1.35) ** 2
        paint[blob & cloth] = col
    out = rgb.copy()
    shade = np.clip(.72 + .5 * (L - 30) / 60, .6, 1.15)[..., None]   # 跟着布料的明暗走，褶子还在
    for c, p in enumerate(PAINT):
        m = paint == c
        out[m] = (np.asarray(p, np.float32)[None, :] * shade[m])
    # 漆块边缘压暗一像素，像平涂里的硬边
    pm = paint >= 0; rim = pm & ~erode(pm, 1)
    out[rim] *= .78
    return Image.fromarray(np.dstack([np.clip(out, 0, 255), al]).astype(np.uint8), 'RGBA')

def write(pid, files, meta):
    d = A + 'Parts/' + pid + '/'; os.makedirs(d, exist_ok=True)
    for fname, img in files.items(): img.save(d + fname)
    json.dump(meta, open(d + 'part.json', 'w'), ensure_ascii=False, indent=1)
    print('写入', d, list(files))

P = S['parts']
coat = {'torso.png': splat('torso', 12, 11), 'arm.png': splat('arm', 4, 23, .85), 'arm_far.png': splat('arm_far', 3, 37, .75)}
write('c0', coat, {'scale': S['scale'], 'torso': {'file': 'torso.png', 'pivot': P['torso']['pivot']}, 'shoulder': S['shoulder'], 'shoulderF': S['shoulderF'],
                   'arm': {'file': 'arm.png', 'pivot': P['arm']['pivot'], 'hand': P['arm']['hand']}, 'arm_far': {'file': 'arm_far.png', 'pivot': P['arm_far']['pivot'], 'hand': P['arm_far']['hand']}})
legs = {'leg_near.png': splat('leg_near', 4, 41, .9, 70), 'leg_far.png': splat('leg_far', 3, 53, .85, 70)}
write('l0', legs, {'scale': S['scale'], 'leg_near': {'file': 'leg_near.png', 'pivot': P['leg_near']['pivot']}, 'leg_far': {'file': 'leg_far.png', 'pivot': P['leg_far']['pivot']}})
