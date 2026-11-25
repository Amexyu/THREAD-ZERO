# 背包：按初始背包的高度缩放，贴背一侧（右边缘）对齐初始背包的右边缘，竖直方向中心对齐。用法：python3 fit_pack.py b81 b82 … [--h 1.0]
import sys, os, json, numpy as np
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))   # 仓库根目录（本脚本在 Gemini/卡32_三品牌实装部件/）
A = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/')
args = sys.argv[1:]; hk = 1.0
if '--h' in args: i = args.index('--h'); hk = float(args[i + 1]); del args[i:i + 2]
M = json.load(open(A + 'Starter/parts.json')); sp = M['parts']['pack']; S = M['scale']
st = np.asarray(Image.open(A + 'Starter/pack.png').convert('RGBA'))[..., 3] > 40; ys, xs = np.nonzero(st)
s_right = (xs.max() + 1) / S - sp['pivot'][0]; s_cy = (ys.min() + ys.max() + 1) / 2 / S - sp['pivot'][1]; s_h = ys.max() - ys.min() + 1
for pid in args:
    d = A + 'Parts/' + pid + '/'; j = json.load(open(d + 'part.json')); e = j['pack']; sc = j.get('scale', 4)
    im = Image.open(d + e['file']).convert('RGBA'); al = np.asarray(im)[..., 3] > 40; ys, xs = np.nonzero(al)
    s = hk * s_h / (ys.max() - ys.min() + 1)
    new = im.convert('RGBa').resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS).convert('RGBA')
    al = np.asarray(new)[..., 3] > 40; ys, xs = np.nonzero(al)
    right = (xs.max() + 1) / sc; cy = (ys.min() + ys.max() + 1) / 2 / sc
    piv = [round(right - s_right, 2), round(cy - s_cy, 2)]
    new.save(d + e['file']); e['pivot'] = piv; json.dump(j, open(d + 'part.json', 'w'), indent=1)
    print(pid, 'scale', round(s, 2), new.size, 'pivot', piv)
