import sys, numpy as np
R_LIST = [float(a) for a in sys.argv[1:]] or [3.6, 5.2]
sys.argv = ['x', '部件A4', 'out']
import run_ref as R, skin
from PIL import Image, ImageDraw, ImageFont
F = ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', 18, index=2)
rows = []
for r in R_LIST:
    R.FILLET = r > 0; r = abs(r)
    for s in (0, 1):
        sh = 1.0 if s == 0 else R.m.get('far_shade', .72)
        R.STRIPS['leg' + R.SFX[s]] = skin.Strip(R.os.path.join(R._SD, R._SM['leg']['file']), R._SM['leg']['joints'], R._SM['scale'], r0=3.0, r1=r, shade=sh)
    tiles = []
    for i, s in ((9, 0), (10, 0), (11, 0), (12, 0), (0, 1), (1, 1), (2, 1)):
        u = i / R.N; p = R.pose(u); img = R.render(p).convert('RGB'); b = R.build(p); scr = R.T(R.CW / 2 - 10, R.CH - 50) @ R.S(R.PX, R.PX)
        q = scr @ b['shin' + R.SFX[s]] @ np.array([0, 0, 1.]); x, y = q[:2]
        c = img.crop((int(x) - 40, int(y) - 40, int(x) + 40, int(y) + 40)).resize((240, 240), Image.LANCZOS); tiles.append(c)
    row = Image.new('RGB', (90 + 244 * len(tiles), 244), 'white'); ImageDraw.Draw(row).text((4, 100), ('圆角' if R.FILLET else '权重') + '%.1f' % r, fill=(0, 0, 0), font=F)
    for j, t in enumerate(tiles): row.paste(t, (90 + j * 244, 2))
    rows.append(row)
out = Image.new('RGB', (rows[0].width, 244 * len(rows)), 'white')
for j, r_ in enumerate(rows): out.paste(r_, (0, 244 * j))
out.save('out/_knee_zoom.png'); print(out.size)
