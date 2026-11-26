# 卡37：把切好的头部件缩放后，用「下巴」对齐初始头的下巴线（不是用最下面的耳机对齐，那样会把脸顶上去、像长了一截脖子）。
# 用法：python3 fit_head.py <src_id> <dst_id> <缩放> <下巴行(源部件像素)> [--lift 单位]
import sys, os, json, numpy as np
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ART = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/')
src, dst, s, chin = sys.argv[1], sys.argv[2], float(sys.argv[3]), int(sys.argv[4])
lift = float(sys.argv[sys.argv.index('--lift') + 1]) if '--lift' in sys.argv else 0.0
M = json.load(open(ART + 'Starter/parts.json')); SP = M['parts']['head']
st = np.asarray(Image.open(ART + 'Starter/head.png').convert('RGBA'))[..., 3] > 40
ys, xs = np.nonzero(st); S = M['scale']
R_target = np.array([(xs.min() + xs.max()) / 2 / S - SP['pivot'][0], ys.max() / S - SP['pivot'][1] - lift])   # 初始头的下巴线（头骨骼坐标）
d = ART + 'Parts/' + src + '/'; j = json.load(open(d + 'part.json')); sc = j.get('scale', 4)
im = Image.open(d + j['head']['file']).convert('RGBA')
al = np.asarray(im)[..., 3] > 40
top = al[:chin]; ys2, xs2 = np.nonzero(top)
A = np.array([(xs2.min() + xs2.max()) / 2 / sc, chin / sc])     # 头发 + 脸（下巴以上）的水平中心、下巴
new = im.convert('RGBa').resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS).convert('RGBA')
piv = s * A - R_target
od = ART + 'Parts/' + dst + '/'; os.makedirs(od, exist_ok=True); new.save(od + 'head.png')
json.dump({'scale': sc, 'head': {'file': 'head.png', 'pivot': [round(float(piv[0]), 2), round(float(piv[1]), 2)]}}, open(od + 'part.json', 'w'), indent=1)
print(dst, new.size, 'pivot', piv.round(2), 'chin target', R_target.round(2))
