"""补洞：Gemini 补接缝时只补了一部分、还留着绿色的时候用。把剩下的绿色当洞（稍微扩大一圈），
重新做一张输入图交给 Gemini（同一个对话里），拿回来对齐以后只把洞里的像素贴回去（边缘羽化）。
用法：
  holefill.py prep <图.png> <输出.jpg> [--roll]          → 输出.jpg、输出.jpg.json、输出.jpg.mask.png
  holefill.py fit  <Gemini 出图> <输出.jpg.json> <结果.png>
--roll：图是原方向（接缝在两头），先左右对调再找洞（tile.py fit 的结果就是原方向）"""
import sys, json, numpy as np, cv2
from PIL import Image
GREEN = np.array([0, 255, 0], np.float32)
def isgreen(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (g > 180) & (r < 110) & (b < 110) & (g - np.maximum(r, b) > 90)
def prep(src, out, roll):
    a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32); H, W = a.shape[:2]
    if roll: a = np.roll(a, W // 2, axis=1)
    m = isgreen(a).astype(np.uint8)
    m = cv2.dilate(m, np.ones((7, 7), np.uint8))
    rgb = a.copy(); rgb[m > 0] = GREEN
    cw = max(W, round(H * 16 / 9)); ch = max(H, round(cw * 9 / 16)); cw = max(cw, round(ch * 16 / 9))
    canvas = np.zeros((ch, cw, 3), np.float32); canvas[:] = (255, 0, 255)
    ox, oy = (cw - W) // 2, (ch - H) // 2
    canvas[oy:oy + H, ox:ox + W] = rgb
    Image.fromarray(canvas.astype(np.uint8)).save(out, quality=95)
    Image.fromarray(m * 255).save(out + '.mask.png')
    json.dump(dict(src=src, roll=roll, W=W, H=H, cw=cw, ch=ch, ox=ox, oy=oy), open(out + '.json', 'w'))
    print(out, '洞', int(m.sum()), '像素')
def fit(gem, meta, out):
    M = json.load(open(meta)); W, H = M['W'], M['H']
    a = np.asarray(Image.open(M['src']).convert('RGB')).astype(np.float32)
    if M['roll']: a = np.roll(a, W // 2, axis=1)
    m = np.asarray(Image.open(meta[:-5] + '.mask.png')).astype(np.float32) / 255
    g = np.asarray(Image.open(gem).convert('RGB').resize((M['cw'], M['ch']), Image.LANCZOS)).astype(np.float32)
    g = g[M['oy']:M['oy'] + H, M['ox']:M['ox'] + W]
    def edges(rgb):
        q = cv2.cvtColor(np.clip(rgb, 0, 255).astype(np.uint8), cv2.COLOR_RGB2GRAY).astype(np.float32)
        gx = cv2.Sobel(q, cv2.CV_32F, 1, 0, ksize=3); gy = cv2.Sobel(q, cv2.CV_32F, 0, 1, ksize=3)
        e = np.sqrt(gx * gx + gy * gy); e = e / (np.percentile(e, 99) + 1e-3)
        return cv2.GaussianBlur(np.clip(e, 0, 1.5), (0, 0), 2)
    keep = (cv2.dilate((m > 0).astype(np.uint8), np.ones((41, 41), np.uint8)) == 0).astype(np.uint8)
    s = .4; ta = cv2.resize(edges(a), None, fx=s, fy=s); tb = cv2.resize(edges(g), None, fx=s, fy=s)
    mk = cv2.resize(keep, None, fx=s, fy=s, interpolation=cv2.INTER_NEAREST)
    w = np.eye(2, 3, dtype=np.float32)
    try: cc, w = cv2.findTransformECC(ta, tb, w, cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 300, 1e-6), mk, 5)
    except cv2.error as e: print('ECC 失败', e); cc = 0
    w[:, 2] /= s
    g = cv2.warpAffine(g, w, (W, H), flags=cv2.INTER_LINEAR + cv2.WARP_INVERSE_MAP, borderMode=cv2.BORDER_REPLICATE)
    print('对齐 ECC %.3f 平移 %.1f,%.1f' % (cc, w[0, 2], w[1, 2]))
    wm = cv2.GaussianBlur(cv2.dilate((m > 0).astype(np.uint8), np.ones((9, 9), np.uint8)).astype(np.float32), (0, 0), 3)
    wm = np.maximum(wm, m)[..., None]
    res = a * (1 - wm) + g * wm
    print('洞里残留的绿色：%.2f%%' % (isgreen(g)[m > 0].mean() * 100))
    if M['roll']: res = np.roll(res, -(W // 2), axis=1)
    Image.fromarray(np.clip(res, 0, 255).astype(np.uint8)).save(out)
    print('写出', out)
if __name__ == '__main__':
    if sys.argv[1] == 'prep': prep(sys.argv[2], sys.argv[3], '--roll' in sys.argv)
    else: fit(sys.argv[2], sys.argv[3], sys.argv[4])
