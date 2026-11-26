# 关节放大检查：每帧把 髋/膝/踝（两条腿）、肩/肘/腕（近侧手）、脖子 各裁一块，放大 2 倍拼成一张
import sys, numpy as np
sys.argv = ['x', '部件A4', 'out']
import run_ref as R
from PIL import Image, ImageDraw, ImageFont
F = ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', 16, index=2)
frames = [int(a) for a in (sys.argv_extra if hasattr(sys, 'argv_extra') else [])] or list(range(R.N))
def jpos(b, scr, k, local=(0, 0)): q = scr @ b[k] @ np.array([local[0], local[1], 1.]); return q[:2]
rows = []
for i in frames:
    u = i / R.N; p = R.pose(u); img = R.render(p).convert('RGB'); b = R.build(p); scr = R.T(R.CW / 2 - 10, R.CH - 50) @ R.S(R.PX, R.PX)
    pts = [('近髋', jpos(b, scr, 'thigh')), ('近膝', jpos(b, scr, 'shin')), ('近踝', jpos(b, scr, 'foot')),
           ('远髋', jpos(b, scr, 'thigh_far')), ('远膝', jpos(b, scr, 'shin_far')), ('远踝', jpos(b, scr, 'foot_far')),
           ('肩', jpos(b, scr, 'upper')), ('肘', jpos(b, scr, 'fore')), ('腕', jpos(b, scr, 'hand')), ('脖子', jpos(b, scr, 'head'))]
    tiles = []
    for nm, (x, y) in pts:
        c = img.crop((int(x) - 30, int(y) - 30, int(x) + 30, int(y) + 30)).resize((120, 120), Image.LANCZOS)
        t = Image.new('RGB', (120, 140), 'white'); t.paste(c, (0, 20)); ImageDraw.Draw(t).text((4, 0), nm, fill=(0, 0, 0), font=F); tiles.append(t)
    row = Image.new('RGB', (60 + 122 * len(tiles), 140), 'white'); ImageDraw.Draw(row).text((4, 50), '%d帧' % (i + 1), fill=(0, 0, 0), font=F)
    for j, t in enumerate(tiles): row.paste(t, (60 + j * 122, 0))
    rows.append(row)
out = Image.new('RGB', (rows[0].width, 140 * len(rows)), 'white')
for j, r in enumerate(rows): out.paste(r, (0, 140 * j))
out.save('out/_joints_zoom.png'); print(out.size)
