# 卡39：把新角色（侧面）放进漫画涂鸦风行动页的概念图里看大小和头身比（原来的角色先用旁边的墙面盖掉）。只做例图。
import numpy as np
from PIL import Image, ImageDraw, ImageFont
base = Image.open('../卡38_界面风格方向/出图/漫画涂鸦_行动页_原图.jpeg').convert('RGB'); W, H = base.size
a = np.asarray(base).copy()
# 旧角色范围（原图坐标）：x 985–1205，y 840–1205；用左边 x 760–980 的同一高度的墙面 / 水道 / 地砖盖住
x0, x1, y0, y1 = 985, 1205, 838, 1206
w = x1 - x0; src = a[y0:y1, 760:760 + w].copy()
a[y0:y1, x0:x1] = src
# 接缝处左右各 10 像素做渐变，别留硬边
for k in range(12):
    t = (k + 1) / 13
    a[y0:y1, x0 + k] = (a[y0:y1, x0 + k] * t + np.asarray(base)[y0:y1, x0 + k] * (1 - t)).astype(np.uint8)
    a[y0:y1, x1 - 1 - k] = (a[y0:y1, x1 - 1 - k] * t + np.asarray(base)[y0:y1, x1 - 1 - k] * (1 - t)).astype(np.uint8)
clean = Image.fromarray(a)
clean.save('出图/_行动页_去掉旧角色.jpg', quality=92)
feet = (1093, 1196); old_h = 345
f = ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', 44)
outs = []
for n, label in (('方案1_喷漆小子', '① 喷漆小子'), ('方案2_耳机回收员', '② 耳机回收员'), ('方案3_喷罐机修', '③ 喷罐机修')):
    s = Image.open(f'出图/{n}_side.png').convert('RGBA')
    hgt = round(old_h * 1.08); s2 = s.resize((round(s.width * hgt / s.height), hgt), Image.LANCZOS)
    al = np.asarray(s2)[..., 3] > 40; cols = np.nonzero(al[-int(hgt * .08):].any(0))[0]   # 脚的水平中心对齐原来的脚
    cx = (cols.min() + cols.max()) / 2
    im = clean.convert('RGBA')
    sh = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(sh)
    d.ellipse([feet[0] - 95, feet[1] - 14, feet[0] + 95, feet[1] + 12], fill=(30, 36, 60, 70)); im.alpha_composite(sh)
    im.alpha_composite(s2, (int(feet[0] - cx), feet[1] - hgt))
    im = im.convert('RGB'); dr = ImageDraw.Draw(im)
    dr.rectangle([2230, 200, 2230 + 420, 200 + 70], fill=(255, 230, 60), outline=(0, 0, 0), width=6); dr.text((2250, 204), label, fill=(0, 0, 0), font=f)
    im.save(f'出图/_例图_行动页_{n}.jpg', quality=90); outs.append(im)
print('ok', len(outs))
