"""接缝（不靠 Gemini）：和 periodcut 一样找一个宽度 P，但重叠区不做交叉淡化，
而是按 Efros-Freeman 的“最小误差边界”逐行找一条弯曲的切线：切线左边用“接在尾巴后面”的像素，右边用“开头”的像素。
屋顶、树林这类杂乱的纹理用它，比交叉淡化少很多重影。
用法: quiltcut.py in.png out.png [--c 220] [--lo 0.75] [--feather 4]"""
import sys, numpy as np, cv2
from PIL import Image
def run(src, dst, c=220, lo=.75, feather=4):
    im = Image.open(src); mode = im.mode
    a = np.asarray(im.convert('RGBA')).astype(np.float32); H, W = a.shape[:2]
    ref = a[:, :c, :3]
    best = None
    for P in range(int(lo * W), W - c):
        d = np.abs(a[:, P:P + c, :3] - ref).mean()
        if best is None or d < best[0]: best = (d, P)
    d, P = best
    A = a[:, P:P + c]; B = a[:, :c]
    e = ((A[..., :3] - B[..., :3]) ** 2).sum(2)
    e = cv2.GaussianBlur(e, (0, 0), 1.5)
    # 两头留一点余地：切线不能贴着重叠区的边
    m = 12; e[:, :m] += 1e9; e[:, c - m:] += 1e9
    cost = e.copy(); back = np.zeros((H, c), np.int32)
    for y in range(1, H):
        prev = cost[y - 1]
        l = np.r_[np.inf, prev[:-1]]; r = np.r_[prev[1:], np.inf]
        stack = np.stack([l, prev, r]); k = stack.argmin(0)
        cost[y] += stack[k, np.arange(c)]; back[y] = np.arange(c) + k - 1
    cut = np.zeros(H, np.int32); cut[-1] = int(cost[-1].argmin())
    for y in range(H - 1, 0, -1): cut[y - 1] = back[y, cut[y]]
    xs = np.arange(c)[None, :]
    w = np.clip((xs - cut[:, None] + feather) / (2 * feather), 0, 1)[..., None]   # 0 = A（尾巴后面）, 1 = B（开头）
    O = A * (1 - w) + B * w
    out = a[:, :P].copy(); out[:, :c] = O
    o = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(o if mode == 'RGBA' else o[..., :3], 'RGBA' if mode == 'RGBA' else 'RGB').save(dst)
    print(dst, '宽', P, '差', round(float(d), 2), '切线范围', int(cut.min()), int(cut.max()))
if __name__ == '__main__':
    a = sys.argv[1:]
    g = lambda k, d: type(d)(a[a.index(k) + 1]) if k in a else d
    run(a[0], a[1], g('--c', 220), g('--lo', .75), g('--feather', 4))
