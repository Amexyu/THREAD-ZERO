# 新设计的图层做成左右无缝：先把图左右对调（接缝挪到正中间），在接缝上盖一条纯绿色竖条，交给 Gemini 补画；
# 拿回来对齐，只取竖条那一段（两边羽化）贴回去，再对调回来。图的左右两头本来就是原图中间连着的内容，所以首尾一定接得上。
# 用法：
#   python3 tile.py prep <图层.png> <输出.jpg> [--band 0.14]          → 输出.jpg 和 输出.jpg.json
#   python3 tile.py fit  <Gemini 出图> <输出.jpg.json> <结果.png> [--alpha]
#     --alpha：图层有透明（品红背景），补画的竖条里也按品红抠
#     --union 像素：和竖条内容连在一起、原图那里透明的 Gemini 像素整块取过来（竖条两边各最多这么多像素），避免画出竖条的东西被羽化成半透明
#     --rows 0.26-0.78：只用这几行做对齐（Gemini 在品红区乱加了东西时）
import sys, json, numpy as np, cv2
UNION = 0
ROWS = None
from PIL import Image
import os; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'bg')); from key2 import key2   # Tools/bg/key2.py

GREEN = np.array([0, 255, 0], np.float32)
MAG = np.array([255, 0, 255], np.float32)

def load_rgba(p): return np.asarray(Image.open(p).convert('RGBA')).astype(np.float32)

def prep(src, out, band):
    a = load_rgba(src); H, W = a.shape[:2]
    rolled = np.roll(a, W // 2, axis=1)
    al = rolled[..., 3:4] / 255
    rgb = rolled[..., :3] * al + MAG * (1 - al)
    bw = int(round(band * W)); x0 = W // 2 - bw // 2
    rgb[:, x0:x0 + bw] = GREEN
    cw = max(W, round(H * 16 / 9)); ch = max(H, round(cw * 9 / 16)); cw = max(cw, round(ch * 16 / 9))
    canvas = np.zeros((ch, cw, 3), np.float32); canvas[:] = MAG
    ox, oy = (cw - W) // 2, (ch - H) // 2
    canvas[oy:oy + H, ox:ox + W] = rgb
    Image.fromarray(canvas.astype(np.uint8)).save(out, quality=95)
    json.dump(dict(src=src, W=W, H=H, cw=cw, ch=ch, ox=ox, oy=oy, x0=x0, bw=bw), open(out + '.json', 'w'), ensure_ascii=False)
    print(out, (cw, ch), 'band', x0, bw)

def edges(rgb):
    g = cv2.cvtColor(np.clip(rgb, 0, 255).astype(np.uint8), cv2.COLOR_RGB2GRAY).astype(np.float32)
    gx = cv2.Sobel(g, cv2.CV_32F, 1, 0, ksize=3); gy = cv2.Sobel(g, cv2.CV_32F, 0, 1, ksize=3)
    m = np.sqrt(gx * gx + gy * gy); m = m / (np.percentile(m, 99) + 1e-3)
    return cv2.GaussianBlur(np.clip(m, 0, 1.5), (0, 0), 2)

def fit(gem, meta, out, use_alpha):
    M = json.load(open(meta)); W, H, x0, bw = M['W'], M['H'], M['x0'], M['bw']
    a = load_rgba(M['src']); rolled = np.roll(a, W // 2, axis=1)
    g = np.asarray(Image.open(gem).convert('RGB').resize((M['cw'], M['ch']), Image.LANCZOS)).astype(np.float32)
    g = g[M['oy']:M['oy'] + H, M['ox']:M['ox'] + W]
    # 对齐：只看竖条以外的地方
    al = rolled[..., 3:4] / 255
    ref = rolled[..., :3] * al + MAG * (1 - al)
    fa, fb = edges(ref), edges(g)
    mask = np.ones((H, W), np.uint8); mask[:, max(0, x0 - 20):x0 + bw + 20] = 0
    if ROWS: mask[:int(ROWS[0] * H)] = 0; mask[int(ROWS[1] * H):] = 0   # 只用这几行对齐（Gemini 在别处乱加了东西时）
    s = .4; ta = cv2.resize(fa, None, fx=s, fy=s); tb = cv2.resize(fb, None, fx=s, fy=s); mk = cv2.resize(mask, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
    w = np.eye(2, 3, dtype=np.float32)
    try: cc, w = cv2.findTransformECC(ta, tb, w, cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 300, 1e-6), mk, 5)
    except cv2.error as e: print('ECC 失败', e); cc = 0
    w[:, 2] /= s
    g = cv2.warpAffine(g, w, (W, H), flags=cv2.INTER_LINEAR + cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REPLICATE)
    print('对齐 ECC %.3f 平移 %.1f,%.1f 缩放 %.3f,%.3f' % (cc, w[0, 2], w[1, 2], w[0, 0], w[1, 1]))
    if use_alpha:
        ka = key2(g); g_rgb = ka[..., :3].astype(np.float32); g_a = ka[..., 3].astype(np.float32) / 255
    else:
        g_rgb = g; g_a = np.ones((H, W), np.float32)
    # 竖条 + 两边各 f 像素羽化
    f = max(12, bw // 6)
    xs = np.arange(W, dtype=np.float32)
    wgt = np.clip(np.minimum(xs - (x0 - f), (x0 + bw + f) - xs) / f, 0, 1)[None, :]
    res_rgb = rolled[..., :3] * (1 - wgt[..., None]) + g_rgb * wgt[..., None]
    res_a = rolled[..., 3] / 255 * (1 - wgt) + g_a * wgt
    if use_alpha and UNION:
        # Gemini 常把竖条里的东西画出竖条外（原图那里是透明的），羽化会把它淡成半透明。
        # 这里把“和竖条里的内容连在一起、原图那里透明”的 Gemini 像素整块取过来（竖条两边各最多 UNION 像素）。
        o_a = rolled[..., 3] / 255
        reach = np.zeros((H, W), bool); reach[:, max(0, x0 - UNION):min(W, x0 + bw + UNION)] = True
        solid = (g_a > .5)
        n, lab = cv2.connectedComponents(solid.astype(np.uint8), connectivity=8)
        inband = np.unique(lab[:, x0 + 4:x0 + bw - 4][solid[:, x0 + 4:x0 + bw - 4]])
        keep = np.isin(lab, inband[inband > 0]) & reach & (o_a < .15)
        res_rgb = np.where(keep[..., None], g_rgb, res_rgb)
        res_a = np.where(keep, g_a, res_a)
        # 反过来：原图有东西、Gemini 那里透明的羽化区，保留原图
        lose = (wgt > 0) & (wgt < 1) & (o_a > .85) & (g_a < .15)
        res_rgb = np.where(lose[..., None], rolled[..., :3], res_rgb)
        res_a = np.where(lose, o_a, res_a)
        print('并集：取 Gemini 像素 %d，保留原图像素 %d' % (int(keep.sum()), int(lose.sum())))
    # 竖条里要是还有绿色（Gemini 没补全），报出来
    left = ((g_rgb[:, x0:x0 + bw, 1] > 200) & (g_rgb[:, x0:x0 + bw, 0] < 80) & (g_rgb[:, x0:x0 + bw, 2] < 80)).mean()
    print('竖条里残留的绿色：%.2f%%' % (left * 100))
    out_rgba = np.dstack([res_rgb, res_a * 255])
    out_rgba = np.roll(out_rgba, -(W // 2), axis=1)
    img = np.clip(out_rgba, 0, 255).astype(np.uint8)
    if use_alpha: Image.fromarray(img, 'RGBA').save(out)
    else: Image.fromarray(img[..., :3], 'RGB').save(out)
    print('写出', out, (W, H))

if __name__ == '__main__':
    if sys.argv[1] == 'prep':
        band = float(sys.argv[sys.argv.index('--band') + 1]) if '--band' in sys.argv else .14
        prep(sys.argv[2], sys.argv[3], band)
    else:
        if '--union' in sys.argv: UNION = int(sys.argv[sys.argv.index('--union') + 1])
        if '--rows' in sys.argv: ROWS = tuple(map(float, sys.argv[sys.argv.index('--rows') + 1].split('-')))
        fit(sys.argv[2], sys.argv[3], sys.argv[4], '--alpha' in sys.argv)
