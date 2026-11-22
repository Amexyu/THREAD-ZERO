# 主角身上的叠加件（机械飞升的散热鳍、线缆……）：一张 Gemini 图（品红底、单个物件）→ 一张透明 PNG，写进 ascension.json。
# 用法：python3 overlay_part.py <图> <名字> --height <像素> --pivot <横向比例> <纵向比例> --at <x> <y> [--json <ascension.json>] [--out 目录]
#   --height：缩放后物件的高度（像素，部件坐标 ×4）；--pivot：枢轴在物件包围盒里的位置（0..1，比如散热鳍的底座贴背处）；
#   --at：枢轴放在挂载骨骼（散热鳍挂背包）的哪个位置（部件坐标）。缩小后补一圈 2 像素深色描边。
import sys, os, json, numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.expanduser('~/mnt/THREAD-ZERO/')
args = sys.argv
src, name = args[1], args[2]
def opt(flag, n=1, cast=float, default=None):
    if flag not in args: return default
    i = args.index(flag); v = [cast(x) for x in args[i + 1:i + 1 + n]]; return v if n > 1 else v[0]
height = opt('--height'); pivot = opt('--pivot', 2); at = opt('--at', 2)
out = opt('--out', cast=str, default=ROOT + 'ThreadZero/Assets/Resources/Art/V16/Ascension/')
jpath = opt('--json', cast=str, default=out + 'ascension.json')

def key(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    score = np.minimum(r, b) - g
    alpha = np.clip((190 - score) / 110, 0, 1)
    ex = np.clip(score, 0, None); k = np.clip((score - 8) / 40, 0, 1)
    return np.dstack([r - ex * k, g, b - ex * k, alpha * 255]).clip(0, 255)

a = key(np.asarray(Image.open(src).convert('RGB')).astype(np.float32))
m = a[..., 3] > 40
ys, xs = np.nonzero(m)
x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
im = Image.fromarray(a[y0:y1 + 1, x0:x1 + 1].astype(np.uint8), 'RGBA')
f = height / im.height
w, h = max(1, round(im.width * f)), max(1, round(im.height * f))
im = im.resize((w, h), Image.LANCZOS)
pad = 8
canvas = Image.new('RGBA', (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
alpha = im.split()[3].point(lambda v: 255 if v > 60 else 0)
outline = Image.new('RGBA', im.size, (22, 22, 24, 255)); outline.putalpha(alpha.filter(ImageFilter.MaxFilter(5)))
canvas.alpha_composite(outline, (pad, pad)); canvas.alpha_composite(im, (pad, pad))
os.makedirs(out, exist_ok=True)
canvas.save(out + name + '.png')
pv = [round((pad + pivot[0] * w) / 4, 2), round((pad + pivot[1] * h) / 4, 2)]
d = json.load(open(jpath)) if os.path.exists(jpath) else {"scale": 4}
d[name] = {"file": name + ".png", "pivot": pv}
if at is not None: d[name + "_at"] = [round(at[0], 2), round(at[1], 2)]
json.dump(d, open(jpath, 'w'), ensure_ascii=False, indent=1)
print(name, canvas.size, 'pivot', pv, 'at', at, '→', out)
