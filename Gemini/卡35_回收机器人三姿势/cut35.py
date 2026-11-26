# 卡35：Gemini 三姿势图 → 先把背景（渐变品红、地面阴影）统一成纯品红，再交给 Tools/bg/pose_sheet.py 切成三张 512 透明图。
# 用法：python3 cut35.py <原图> <怪名> [参考怪，默认同名] [--fit] [--height N] [--spill] [--game]（--game：直接写进游戏的 Enemies 目录；默认写到 切图/）
import sys, os, subprocess, numpy as np
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
src, name = sys.argv[1], sys.argv[2]
ref = sys.argv[3] if len(sys.argv) > 3 and not sys.argv[3].startswith('--') else name
a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
if '--flip' in sys.argv:   # --flip 3（或 2,3）：把朝右的那几格左右翻过来（格号从 1 数）
    W0 = a.shape[1]
    for k in sys.argv[sys.argv.index('--flip') + 1].split(','):
        i = int(k) - 1; x0, x1 = i * W0 // 3, (i + 1) * W0 // 3; a[:, x0:x1] = a[:, x0:x1][:, ::-1].copy()
        print('第', k, '格左右翻转')
r, g, b = a[..., 0], a[..., 1], a[..., 2]
if '--green' in sys.argv:   # 绿底（#00FF00）：身上有霓虹粉、紫的怪用绿底出图，这里按「绿色程度」算背景，再去掉边上的绿溢色
    m = g - np.maximum(r, b)
    bg = np.clip((m - 30) / 40, 0, 1)[..., None]
    a[..., 1] = np.minimum(g, np.maximum(r, b) + 12); r, g, b = a[..., 0], a[..., 1], a[..., 2]
else:
    m = np.minimum(r, b) - g; h = np.abs(r - b)
    bg = np.clip((m - .6 * h - 30) / 40, 0, 1)[..., None]          # 品红程度：纯品红、浅品红渐变、深品红阴影都算背景
out = bg * np.array([255, 0, 255], np.float32) + (1 - bg) * a
out[bg[..., 0] > .5] = (255, 0, 255)                         # 一半以上是背景的直接刷成纯品红（格子线的淡边不会漏成半透明）
def label(m):
    """8 连通标号（Mac 上没有 scipy，自己写）。"""
    lab = np.zeros(m.shape, np.int32); n = 0; H, W = m.shape
    for y0, x0 in zip(*np.nonzero(m)):
        if lab[y0, x0]: continue
        n += 1; lab[y0, x0] = n; st = [(y0, x0)]
        while st:
            y, x = st.pop()
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < H and 0 <= xx < W and m[yy, xx] and not lab[yy, xx]: lab[yy, xx] = n; st.append((yy, xx))
    return lab, n
def dilate(m, it):
    m = m.copy()
    for _ in range(it):
        g = m.copy(); g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]; m = g
    return m
# 去掉地面线：横向连续很长（超过格宽 35%）、竖向又很薄（不到 10 像素）的前景像素刷成品红；脚底压着线的地方竖向厚，会留下
fgm = bg[..., 0] < .5; H, W = fgm.shape; line = np.zeros_like(fgm)
for y in range(H // 2, H):
    row = fgm[y]; xs = np.flatnonzero(np.diff(np.concatenate([[0], row.astype(np.int8), [0]])))
    for a0, a1 in zip(xs[::2], xs[1::2]):
        if a1 - a0 > .35 * W / 3: line[y, a0:a1] = True
if line.any():
    up = np.zeros_like(fgm); dn = np.zeros_like(fgm)
    for k in range(1, 11):
        up[k:] |= ~fgm[:-k] if False else False
    thick = np.zeros(fgm.shape, np.int16)
    for k in range(-10, 11):
        sh = np.zeros_like(fgm)
        if k >= 0: sh[:H - k] = fgm[k:]
        else: sh[-k:] = fgm[:H + k]
        thick += sh
    kill = line & (thick < 12)
    out[kill] = (255, 0, 255); bg[kill] = 1
    if kill.any(): print('去掉地面线', int(kill.sum()), '像素')
# 去掉格子分隔竖线：竖向连续超过图高 35%、横向不到 12 像素厚的前景
fgm = bg[..., 0] < .5; vline = np.zeros_like(fgm)
for x in range(W):
    col = fgm[:, x]; ys = np.flatnonzero(np.diff(np.concatenate([[0], col.astype(np.int8), [0]])))
    for a0, a1 in zip(ys[::2], ys[1::2]):
        if a1 - a0 > .35 * H: vline[a0:a1, x] = True
if vline.any():
    thick = np.zeros(fgm.shape, np.int16)
    for k in range(-10, 11):
        sh = np.zeros_like(fgm)
        if k >= 0: sh[:, :W - k] = fgm[:, k:]
        else: sh[:, -k:] = fgm[:, :W + k]
        thick += sh
    kill = vline & (thick < 12)
    out[kill] = (255, 0, 255); bg[kill] = 1
    if kill.any(): print('去掉分隔竖线', int(kill.sum()), '像素')
# --spill：某个姿势伸过了格子边界（毛衣精英出手时的毛衣针）。按连通块分格：每块归到它大部分像素所在的格子，
# 每格左右各多留 pad 宽重新拼成三等分（三格同样的偏移，切出来的位置关系不变），别的格子的块在这里刷成品红
if '--spill' in sys.argv:
    pad = int(.08 * W); fgm = bg[..., 0] < .5; sm = fgm[::4, ::4]
    lab, n = label(dilate(sm, 2)); ys, xs = np.nonzero(lab * sm)   # 先膨胀 2 格再标号：分隔竖线切断的细线（缝纫机的线、毛衣针）还连在主体上
    cnt = np.zeros((n + 1, 3), np.int64); np.add.at(cnt, (lab[ys, xs], np.minimum(2, xs * 4 * 3 // W)), 1)
    owner = cnt.argmax(1); Wc = W // 3 + 2 * pad
    new = np.empty((H, 3 * Wc, 3), np.float32); new[:] = (255, 0, 255); nbg = np.ones((H, 3 * Wc, 1), np.float32)
    for i in range(3):
        other = dilate((lab > 0) & (owner[lab] != i), 1) & ~((lab > 0) & (owner[lab] == i))
        kill = np.repeat(np.repeat(other, 4, 0), 4, 1)[:H, :W]
        sx0 = i * W // 3 - pad; cx0, cx1 = max(0, sx0), min(W, sx0 + Wc); dx = i * Wc + (cx0 - sx0)
        seg = out[:, cx0:cx1].copy(); sbg = bg[:, cx0:cx1].copy(); k = kill[:, cx0:cx1]
        seg[k] = (255, 0, 255); sbg[k] = 1
        new[:, dx:dx + cx1 - cx0] = seg; nbg[:, dx:dx + cx1 - cx0] = sbg
    out, bg = new, nbg; H, W = out.shape[:2]; print('按连通块重新分格（--spill），每格多留', pad, '像素')
# 格子边界两侧 ±0.8% 宽的带子里，横向不到 14 像素厚的前景（断断续续的分隔线）也刷掉
fgm = bg[..., 0] < .5; band = np.zeros_like(fgm)
for xb in (0, W // 3, 2 * W // 3, W):
    band[:, max(0, xb - int(.012 * W)):xb + int(.012 * W)] = True
thick = np.zeros(fgm.shape, np.int16)
for k in range(-10, 11):
    sh = np.zeros_like(fgm)
    if k >= 0: sh[:, :W - k] = fgm[:, k:]
    else: sh[:, -k:] = fgm[:, :W + k]
    thick += sh
kill = band & fgm & (thick < 20)
if kill.any(): out[kill] = (255, 0, 255); bg[kill] = 1; print('去掉格子边界上的细线', int(kill.sum()), '像素')
# 每格只留主体：和最大一块隔开、又小的碎块（标题字、速度线、星星）先刷成品红，免得算进脚底和大小
H, W = out.shape[:2]; fgm = bg[..., 0] < .5
for i in range(3):
    x0, x1 = i * W // 3, (i + 1) * W // 3; sm = fgm[::4, x0:x1:4]
    lab, n = label(sm)
    if not n: continue
    sizes = np.bincount(lab.ravel())[1:]; big = int(np.argmax(sizes)) + 1
    near = dilate(lab == big, 4)
    for k in range(1, n + 1):
        if k == big or sizes[k - 1] >= .15 * sizes.max() or (near & (lab == k)).any(): continue
        ys, xs = np.nonzero(lab == k)
        out[max(0, ys.min() * 4 - 3):ys.max() * 4 + 7, x0 + max(0, xs.min() * 4 - 3):x0 + xs.max() * 4 + 7] = (255, 0, 255)
tmp = os.path.join(HERE, '切图', '_' + name + '_norm.png'); Image.fromarray(out.clip(0, 255).astype(np.uint8)).save(tmp)
args = [sys.executable, os.path.join(ROOT, 'Tools', 'bg', 'pose_sheet.py'), tmp, name, ref]
if '--game' not in sys.argv: args += ['--out', os.path.join(HERE, '切图')]
for k in ('--fit', '--keepbits'):
    if k in sys.argv: args.append(k)
if '--height' in sys.argv: args += ['--height', sys.argv[sys.argv.index('--height') + 1]]
print(subprocess.run(args, capture_output=True, text=True).stdout.strip())
# --ground：原版是飘着的（脚底 ≈ 408），新设计站在地上 → 三张一起往下挪到地面线 448
if '--ground' in sys.argv:
    E = ROOT + '/ThreadZero/Assets/Resources/Art/V16/Enemies/'
    rb = Image.open(E + ref + '_idle.png').getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()
    od = ROOT + '/ThreadZero/Assets/Resources/Art/V16/Enemies' if '--game' in sys.argv else os.path.join(HERE, '切图')
    dy = 448 - rb[3]
    for pose in ('idle', 'windup', 'strike'):
        p = os.path.join(od, name + '_' + pose + '.png'); im = Image.open(p).convert('RGBA')
        c = Image.new('RGBA', im.size, (0, 0, 0, 0)); c.paste(im, (0, dy)); c.save(p)
    print('落地：整体下移', dy, '像素')
# 收尾：离主体 8 像素以外的半透明残渣（文字标签、速度线的淡边）全部清掉
from PIL import ImageFilter
outdir = ROOT + '/ThreadZero/Assets/Resources/Art/V16/Enemies' if '--game' in sys.argv else os.path.join(HERE, '切图')
for pose in ('idle', 'windup', 'strike'):
    p = os.path.join(outdir, name + '_' + pose + '.png'); im = Image.open(p).convert('RGBA'); px = np.asarray(im).copy()
    near = np.asarray(Image.fromarray(((px[..., 3] > 40) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(17))) > 0
    n = int(((px[..., 3] > 0) & ~near).sum()); px[~near, 3] = 0
    Image.fromarray(px, 'RGBA').save(p); bb = Image.fromarray(px, 'RGBA').getbbox()
    if n: print(name, pose, '清掉远处残渣', n, '像素，现在 bbox', bb)
# --align：Gemini 没把三格画在同一条底线上时，按 idle 的脚底把 windup / strike 上下对齐
if '--align' in sys.argv:
    base = Image.open(os.path.join(outdir, name + '_idle.png')).getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()[3]
    for pose in ('windup', 'strike'):
        p = os.path.join(outdir, name + '_' + pose + '.png'); im = Image.open(p).convert('RGBA')
        bt = im.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()[3]
        c = Image.new('RGBA', im.size, (0, 0, 0, 0)); c.paste(im, (0, base - bt)); c.save(p); print(name, pose, '对齐底线：下移', base - bt)
