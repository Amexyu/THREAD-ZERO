# 机械飞升第一阶段的机械手：一张 Gemini 图（左近侧手、右远侧手，品红底）→ 两张透明 PNG + ascension.json（枢轴点）。
# 大小对齐初始手（V13/Starter/hand.png 拳头约 56 像素宽、hand_far.png 约 39 像素宽），手腕上面那截金属圆筒只留一小段，
# 不然会盖住袖口。缩小后描边会变细，额外补一圈 2 像素的深色描边，和其它 Q 版部件的粗描边对上。
# 用法：python3 hand_parts.py <机械手图> [--out 目录]
import sys, os, json, numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.expanduser('~/mnt/THREAD-ZERO/')
src = sys.argv[1]
out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else ROOT + 'ThreadZero/Assets/Resources/Art/V16/Ascension/'
os.makedirs(out, exist_ok=True)


def key(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    score = np.minimum(r, b) - g
    alpha = np.clip((190 - score) / 110, 0, 1)
    ex = np.clip(score, 0, None)
    k = np.clip((score - 8) / 40, 0, 1)
    return np.dstack([r - ex * k, g, b - ex * k, alpha * 255]).clip(0, 255)


def part(a, fist_w, name):
    m = a[..., 3] > 40
    ys, xs = np.nonzero(m)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    widths = m[y0:y1 + 1, x0:x1 + 1].sum(1)
    full = widths.max()
    fist_top = int(np.argmax(widths > .75 * full))            # 从上往下第一行比较宽的：拳头开始（上面是窄的手腕圆筒）
    fist_h = (y1 - y0) - fist_top
    cut = max(0, fist_top - int(fist_h * .12))                 # 手腕圆筒只留拳头高度 12% 的一小截
    crop = a[y0 + cut:y1 + 1, x0:x1 + 1]
    im = Image.fromarray(crop.astype(np.uint8), 'RGBA')
    f = fist_w / full
    w, h = max(1, round(im.width * f)), max(1, round(im.height * f))
    im = im.resize((w, h), Image.LANCZOS)
    pad = 8
    canvas = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    # 补描边：alpha 外扩 2 像素，垫一层深色
    alpha = im.split()[3].point(lambda v: 255 if v > 60 else 0)
    grown = alpha.filter(ImageFilter.MaxFilter(5))
    outline = Image.new('RGBA', im.size, (22, 22, 24, 255))
    outline.putalpha(grown)
    canvas.alpha_composite(outline, (pad, pad))
    canvas.alpha_composite(im, (pad, pad))
    canvas.save(out + name)
    stub = (fist_top - cut) * f
    pivot_px = (canvas.width / 2, pad + stub + (h - stub) / 2)    # 枢轴：拳头中心（初始手也是拳头中心）
    print(name, canvas.size, 'fist', round(full * f), 'pivot px', [round(v, 1) for v in pivot_px])
    return [round(pivot_px[0] / 4, 2), round(pivot_px[1] / 4, 2)]   # 部件坐标 = 像素 / 4（parts.json 的 scale）


a = key(np.asarray(Image.open(src).convert('RGB')).astype(np.float32))
H, W = a.shape[:2]
cols = (a[..., 3] > 40).sum(0)
lo, hi = int(W * .35), int(W * .65)
split = lo + int(np.argmin(cols[lo:hi]))                     # 两只手中间最空的那一列
near = part(a[:, :split], 56, 'hand_mech.png')
far = part(a[:, split:], 39, 'hand_far_mech.png')
json.dump({"scale": 4, "hand": {"file": "hand_mech.png", "pivot": near}, "hand_far": {"file": "hand_far_mech.png", "pivot": far}},
          open(out + 'ascension.json', 'w'), ensure_ascii=False, indent=1)
print('split at', split, '→', out)
