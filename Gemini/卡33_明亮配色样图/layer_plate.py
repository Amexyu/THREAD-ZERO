# 用游戏里的分层原图（远景 / 中景 / 地面）直接拼一张不带角色的底图，代替「截图去掉角色再 inpaint」的底图（那种底图会留涂抹）。
# 排法照 StageArt.Background：舞台逻辑 1600×900（×1.2 = 1920×1080），远景、中景按舞台高缩放、横向平铺，
# 卷动 = worldX × 0.25 / 0.6；地面带高 (900 - groundY) / 0.8，图高 20% 处对齐 groundY，卷动 = worldX。
# worldX、groundY 截图时不知道：拿实机截图里没被角色挡住的背景去比，搜出来。
#   用法: python3 layer_plate.py <Stage 文件夹> <场景 A~D> <实机全图> <黑底> <白底> <输出.jpg>
import sys, os, numpy as np
from PIL import Image
K = 1.2; SW, SH = 1600, 900
def load(folder, key):
    L = {}
    for n in ('far', 'mid', 'ground'):
        p = os.path.join(folder, '%s_%s.png' % (key, n))
        if os.path.exists(p): L[n] = Image.open(p).convert('RGBA')
    return L
def band(tex, scroll, h, y, scale, out):
    # 把一层按高度 h（逻辑）缩放、从 scroll（逻辑）开始横向平铺，贴到 out（scale = 输出像素 / 逻辑像素）
    H = max(1, int(round(h * scale))); W = max(1, int(round(H * tex.width / tex.height)))
    t = tex.resize((W, H), Image.LANCZOS) if (W, H) != tex.size else tex
    ta = np.asarray(t).astype(np.float32)
    ow = out.shape[1]; off = int(round(scroll * scale)) % W
    xs = (np.arange(ow) + off) % W
    strip = ta[:, xs]
    y0 = int(round(y * scale)); y1 = y0 + H
    a0, a1 = max(0, y0), min(out.shape[0], y1)
    if a1 <= a0: return
    s = strip[a0 - y0:a1 - y0]; al = s[..., 3:4] / 255.0
    out[a0:a1, :, :3] = out[a0:a1, :, :3] * (1 - al) + s[..., :3] * al
def scaled(tex, h, scale):
    H = max(1, int(round(h * scale))); W = max(1, int(round(H * tex.width / tex.height)))
    return np.asarray(tex.resize((W, H), Image.LANCZOS)).astype(np.float32)
def put(out, ta, off, y0):
    W = ta.shape[1]; xs = (np.arange(out.shape[1]) + int(off)) % W; strip = ta[:, xs]
    H = ta.shape[0]; a0, a1 = max(0, y0), min(out.shape[0], y0 + H)
    if a1 <= a0: return
    s = strip[a0 - y0:a1 - y0]; al = s[..., 3:4] / 255.0
    out[a0:a1] = out[a0:a1] * (1 - al) + s[..., :3] * al
def cover(out, tex, scale):
    # 首领场地（D）：远景按 Cover 铺满舞台、居中、不卷动（StageArt.Cover）
    k = max(SW / tex.width, SH / tex.height); tw, th = tex.width * k, tex.height * k
    W, H = int(round(tw * scale)), int(round(th * scale)); t = np.asarray(tex.resize((W, H), Image.LANCZOS)).astype(np.float32)
    x0 = int(round((SW - tw) / 2 * scale)); y0 = int(round((SH - th) / 2 * scale))
    oh, ow = out.shape[:2]; sx, sy = max(0, -x0), max(0, -y0)
    out[max(0, y0):max(0, y0) + min(oh, H - sy), max(0, x0):max(0, x0) + min(ow, W - sx)] = t[sy:sy + min(oh, H - sy), sx:sx + min(ow, W - sx), :3]
def render_off(L, offs, groundY, scale, arena=False):
    # offs：每层在输出像素里的横向起点（far, mid, ground），各层单独对齐，不用凑同一个 worldX
    out = np.zeros((int(round(SH * scale)), int(round(SW * scale)), 3), np.float32)
    if arena: cover(out, L['far'], scale)
    else: put(out, scaled(L['far'], SH, scale), offs[0], 0)
    if 'mid' in L: put(out, scaled(L['mid'], SH, scale), offs[1], 0)
    if 'ground' in L:
        h = (SH - groundY) / .8; put(out, scaled(L['ground'], h, scale), offs[2], int(round((groundY - h * .2) * scale)))
    return out
def fit(L, full, black, white, scale=0.3, arena=False):
    W0, H0 = int(round(SW * scale)), int(round(SH * scale))
    F = np.asarray(full.convert('RGB').resize((W0, H0), Image.LANCZOS)).astype(np.float32)
    B = np.asarray(black.convert('RGB')).astype(np.float32); Wt = np.asarray(white.convert('RGB')).astype(np.float32)
    a = 1 - (Wt - B).mean(-1) / 255
    M = np.asarray(Image.fromarray(((a < 0.02) * 255).astype(np.uint8)).resize((W0, H0))) > 250
    M[:int(H0 * .09)] = False   # 最上面一条是界面（关卡名、测试横幅），不算
    # 1) 地面：groundY 以下整块不透明
    best = None
    for gy in range(640, 781, 2):
        h = (SH - gy) / .8; g = scaled(L['ground'], h, scale); y0 = int(round((gy - h * .2) * scale))
        y1 = int(round((gy + 8) * scale)); rows = slice(y1, H0)
        tex_rows = slice(y1 - y0, y1 - y0 + (H0 - y1))
        gt = g[tex_rows]; Wg = gt.shape[1]
        for off in range(0, Wg, 2):
            xs = (np.arange(W0) + off) % Wg; R = gt[:, xs, :3]; mm = M[rows]
            e = np.abs(R - F[rows])[mm].mean()
            if best is None or e < best[0]: best = (e, gy, off)
    eg, gy, og = best
    if arena or 'mid' not in L:
        return dict(groundY=gy, offs=(0, 0, og * K / scale), err=(round(eg, 2),), arena=arena)
    # 2) 中景：地面线以上、中景不透明的地方
    m = scaled(L['mid'], SH, scale); Wm = m.shape[1]; ytop = int(round((gy - 4) * scale))
    bm = None
    for off in range(0, Wm, 1):
        xs = (np.arange(W0) + off) % Wm; R = m[:ytop, xs]; al = R[..., 3] > 240
        mm = M[:ytop] & al
        if mm.sum() < 500: continue
        e = np.abs(R[..., :3] - F[:ytop])[mm].mean()
        if bm is None or e < bm[0]: bm = (e, off)
    em, om = bm
    # 3) 远景：中景透明的地方
    f = scaled(L['far'], SH, scale); Wf = f.shape[1]
    xs = (np.arange(W0) + om) % Wm; mid_al = m[:ytop, xs, 3] < 10
    bf = None
    for off in range(0, Wf, 1):
        xs2 = (np.arange(W0) + off) % Wf; R = f[:ytop, xs2, :3]; mm = M[:ytop] & mid_al
        e = np.abs(R - F[:ytop])[mm].mean()
        if bf is None or e < bf[0]: bf = (e, off)
    ef, of = bf
    k = K / scale
    return dict(groundY=gy, offs=(of * k, om * k, og * k), err=(round(eg, 2), round(em, 2), round(ef, 2)))
def refine(L, r, full, black, white):
    arena = r.get('arena', False)
    # 在输出分辨率上每层 ±8 像素再找一次（粗搜的步长在全尺寸上有 4 像素左右）
    F = np.asarray(full.convert('RGB')).astype(np.float32)
    B = np.asarray(black.convert('RGB')).astype(np.float32); Wt = np.asarray(white.convert('RGB')).astype(np.float32)
    M = (1 - (Wt - B).mean(-1) / 255) < 0.02; M[:int(F.shape[0] * .09)] = False
    offs = list(r['offs']); gy = r['groundY']
    for i in ((2,) if arena else (2, 1, 0)):
        best = None
        for d in range(-8, 9):
            o = list(offs); o[i] = offs[i] + d
            R = render_off(L, o, gy, K, arena); e = np.abs(R - F)[M].mean()
            if best is None or e < best[0]: best = (e, o[i])
        offs[i] = best[1]
    r = dict(r); r['offs'] = tuple(offs); r['err_full'] = round(float(best[0]), 2); return r
if __name__ == '__main__':
    folder, key, fullp, bp, wp, dst = sys.argv[1:7]
    L = load(folder, key)
    arena = key == 'D'
    r = fit(L, Image.open(fullp), Image.open(bp), Image.open(wp), arena=arena)
    r = refine(L, r, Image.open(fullp), Image.open(bp), Image.open(wp)); print(r)
    R = render_off(L, r['offs'], r['groundY'], K, arena)
    Image.fromarray(np.clip(R, 0, 255).astype(np.uint8)).save(dst, quality=95)
    print(dst)
