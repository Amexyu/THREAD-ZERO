# 锁链类武器（借 w7 招式的）：从切好的 weapon.png 里把下端的钩头单独切成 hook.png（战斗里链子 / 线缆是程序画的，末端只贴钩头）。
# 用法：python3 weapon_hook.py w19 176 [fiber|cable|rust]
#   176 = 在 weapon.png 里从第几行（像素，从上往下）开始算钩头；第三个参数是程序画的那段链子的样子（写进 part.json 的 chain）。
import sys, os, json, numpy as np
from PIL import Image
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
d = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/Parts', sys.argv[1]); cut = int(sys.argv[2]); style = sys.argv[3] if len(sys.argv) > 3 else None
im = Image.open(os.path.join(d, 'weapon.png')).convert('RGBA'); j = json.load(open(os.path.join(d, 'part.json'))); sc = j.get('scale', 4)
hook = im.crop((0, cut, im.width, im.height)); a = np.asarray(hook)[..., 3] / 255
cols = np.nonzero(a.sum(0) > .3)[0]; x0, x1 = max(0, cols[0] - 3), min(hook.width, cols[-1] + 4)
hook = hook.crop((x0, 0, x1, hook.height)); a = np.asarray(hook)[..., 3] / 255
top = a[:6]; xs = np.arange(hook.width); px = float((xs * top.sum(0)).sum() / max(1e-6, top.sum()))
hook.save(os.path.join(d, 'hook.png'), optimize=True)
j['hook'] = {'file': 'hook.png', 'pivot': [round(px / sc, 2), 1.0]}
if style: j['chain'] = style
json.dump(j, open(os.path.join(d, 'part.json'), 'w'), ensure_ascii=False)
print(sys.argv[1], 'hook', hook.size, j['hook'], 'chain', j.get('chain'))
