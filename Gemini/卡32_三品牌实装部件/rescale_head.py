# 把头部件整体缩放并把某个锚点（默认：不透明像素的底边中点）放到初始头的下巴线上。
# 用法：python3 rescale_head.py <src_id> <dst_id> <s> [--lift 单位] [--cutbase 行(部件像素)]
import sys, os, json, shutil, numpy as np
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))   # 仓库根目录（本脚本在 Gemini/卡32_三品牌实装部件/）
ART = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/')
src, dst, s = sys.argv[1], sys.argv[2], float(sys.argv[3]); lift = 0.0; cutbase = None
a = sys.argv[4:]
while a:
    if a[0] == '--lift': lift = float(a[1]); a = a[2:]
    elif a[0] == '--cutbase': cutbase = int(a[1]); a = a[2:]
    else: a = a[1:]
M = json.load(open(ART + 'Starter/parts.json')); SP = M['parts']['head']; S = M['scale']
st = np.asarray(Image.open(ART + 'Starter/head.png').convert('RGBA'))[..., 3] > 40
ys, xs = np.nonzero(st); chin_y = ys.max() / S; mid_x = (xs.min() + xs.max()) / 2 / S
R_target = np.array([mid_x - SP['pivot'][0], chin_y - SP['pivot'][1] - lift])   # 下巴线（头骨骼坐标）
d = ART + 'Parts/' + src + '/'; j = json.load(open(d + 'part.json')); sc = j.get('scale', 4)
im = Image.open(d + j['head']['file']).convert('RGBA')
if cutbase is not None: im = im.crop((0, 0, im.width, cutbase))
al = np.asarray(im)[..., 3] > 40; ys, xs = np.nonzero(al)
A = np.array([(xs.min() + xs.max()) / 2 / sc, ys.max() / sc])   # 底边中点（部件单位）
new = im.convert('RGBa').resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.LANCZOS).convert('RGBA')
piv = s * A - R_target
od = ART + 'Parts/' + dst + '/'; os.makedirs(od, exist_ok=True); new.save(od + 'head.png')
json.dump({'scale': sc, 'head': {'file': 'head.png', 'pivot': [round(float(piv[0]), 2), round(float(piv[1]), 2)]}}, open(od + 'part.json', 'w'), indent=1)
print(dst, new.size, 'pivot', piv.round(2), 'chin', R_target.round(2))
