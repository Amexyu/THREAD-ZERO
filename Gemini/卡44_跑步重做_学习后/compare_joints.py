# 关节新旧对照：上面 = 两节拼件（关节有硬线、遮挡处有空洞），下面 = 整条网格变形
import sys, numpy as np
sys.argv = ['x', '部件A4', 'out']
import run_ref as R
from PIL import Image, ImageDraw, ImageFont
F = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', s, index=2)
old_torso = Image.open('../卡42_主角标准站立图/部件A3/torso.png').convert('RGBA'); new_torso = R.P['torso']['img']
old_foot = Image.open('../卡42_主角标准站立图/部件A3/foot.png').convert('RGBA'); new_foot = R.P['foot']['img']
SHOTS = [(3, 'leg_far', 'shin_far', '远侧膝盖（弯 110°）'), (10, 'leg', 'shin', '近侧膝盖（弯 105°）'), (7, 'arm', 'fore', '手肘（弯 100°）'), (16, 'arm', 'upper', '肩膀 + 手肘（往后摆）')]
cells = []
for use_skin in (False, True):
    R.USE_SKIN = use_skin
    R.P['torso']['img'] = new_torso if use_skin else old_torso; R.P['foot']['img'] = new_foot if use_skin else old_foot
    row = []
    for i, part, key, nm in SHOTS:
        p = R.pose(i / 18); img = R.render(p).convert('RGB'); b = R.build(p); scr = R.T(R.CW / 2 - 10, R.CH - 50) @ R.S(R.PX, R.PX)
        q = scr @ b[key] @ np.array([0, 0, 1.]); x, y = q[:2]
        row.append(img.crop((int(x) - 55, int(y) - 55, int(x) + 55, int(y) + 55)).resize((330, 330), Image.LANCZOS))
    cells.append(row)
W = 130 + 340 * len(SHOTS); H = 60 + 2 * 380
out = Image.new('RGB', (W, H), 'white'); d = ImageDraw.Draw(out)
for j, s in enumerate(SHOTS): d.text((130 + j * 340 + 10, 14), s[3], fill=(20, 20, 20), font=F(24))
for r, (nm, col) in enumerate((('两节拼件', (170, 40, 40)), ('整条变形', (20, 120, 60)))):
    y0 = 60 + r * 380
    d.text((10, y0 + 140), nm[:2], fill=col, font=F(30)); d.text((10, y0 + 180), nm[2:], fill=col, font=F(30))
    for j, c in enumerate(cells[r]): out.paste(c, (130 + j * 340, y0))
d.text((130, 60 + 330 + 6), '关节处两块图叠在一起：圆头描边横穿过去成一道硬线；颜料点被抠成半透明', fill=(170, 40, 40), font=F(20))
d.text((130, 60 + 380 + 330 + 6), '一整条贴图在关节处弯：外侧圆、内侧收成一道折痕；透明的地方都补上了', fill=(20, 120, 60), font=F(20))
out.save('out/对照_关节_拼件vs整条变形.png'); print(out.size)
R.USE_SKIN = True
