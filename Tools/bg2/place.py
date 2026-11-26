"""图层摆位（v0.34 第一章重新设计用）。游戏里远景 / 中景都按舞台高 900 等比缩放横向平铺，地面带顶在 637.5（GroundY 690 − 0.2h）。
  中景：等比缩放 s，并让原图的 ya（0..1）落到画布的 yb；画布高 1024，宽跟着缩。上下空出来的地方透明。
      python3 place.py mid  <in.png> <out.png> --s 0.75 --ya 0.7715 --yb 0.712
  远景：整体上移 shift（底下用最后一行补齐，反正被中景和地面挡住），再加一层淡淡的天空色雾（haze）把远景推远。
      python3 place.py far  <in.png> <out.png> --shift 0.27 --haze 0.12
  首领场地（远景铺满不卷动）：等比放大到「步道后沿 walk 落在 0.7083」，居中裁成 16:9，输出 2048×1152。
      python3 place.py cover <in.jpg> <out.png> --walk 0.6797"""
import sys, numpy as np
from PIL import Image
GROUND_TOP = 637.5 / 900
def arg(a, k, d): return float(a[a.index(k) + 1]) if k in a else d
def mid(src, dst, s, ya, yb, H=1024):
    img = Image.open(src).convert('RGBA'); W0, H0 = img.size
    k = H / H0 * s; w, h = round(W0 * k), round(H0 * k)
    t = img.resize((w, h), Image.LANCZOS)
    out = Image.new('RGBA', (w, H), (0, 0, 0, 0)); top = round(yb * H - ya * h)
    out.alpha_composite(t, (0, top)) if top >= 0 else out.alpha_composite(t.crop((0, -top, w, h)), (0, 0))
    a = np.asarray(out).copy(); a[a[..., 3] == 0, :3] = 0   # 透明处 RGB 清零，免得双线性采样把品红带出来
    Image.fromarray(a, 'RGBA').save(dst, optimize=True); print(dst, (w, H))
def far(src, dst, shift, haze):
    f = np.asarray(Image.open(src).convert('RGB')).astype(np.float32); H = f.shape[0]
    k = int(round(shift * H)); f = np.vstack([f[k:], np.repeat(f[-1:], k, 0)])
    hz = np.array([214, 234, 250], np.float32); f = np.clip(f * (1 - haze) + hz * haze, 0, 255).astype(np.uint8)
    Image.fromarray(f).save(dst, optimize=True); print(dst, f.shape[1::-1])
def cover(src, dst, walk):
    im = Image.open(src).convert('RGB'); W, H = im.size
    s = GROUND_TOP / walk; sw, sh = round(W * s), round(H * s)
    big = im.resize((sw, sh), Image.LANCZOS)
    cw = round(H * 16 / 9); x0 = (sw - cw) // 2
    out = big.crop((x0, 0, x0 + cw, H)).resize((2048, 1152), Image.LANCZOS)
    out.save(dst, optimize=True); print(dst, out.size, 'scale', round(s, 4))
if __name__ == '__main__':
    a = sys.argv[1:]; cmd = a[0]
    if cmd == 'mid': mid(a[1], a[2], arg(a, '--s', 1), arg(a, '--ya', .8), arg(a, '--yb', .712))
    elif cmd == 'far': far(a[1], a[2], arg(a, '--shift', 0), arg(a, '--haze', .12))
    elif cmd == 'cover': cover(a[1], a[2], arg(a, '--walk', .68))
