# 旧版（卡43）和新版（卡44）同样五个关键姿势并排
import sys, os, importlib.util
from PIL import Image, ImageDraw, ImageFont
PARTS = '../卡42_主角标准站立图/部件A3'
def load(path, name, argv):
    sys.argv = argv; spec = importlib.util.spec_from_file_location(name, path); mod = importlib.util.module_from_spec(spec); spec.loader.exec_module(mod); return mod
old = load('../卡43_主角走路动画/run_rig.py', 'old', ['x', PARTS])
PARTS_NEW = '部件A4'
new = load('run_ref.py', 'new', ['x', PARTS_NEW, 'out'])
F = lambda sz: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', sz, index=2)
KEY = [('触地', 0.0, '旧：两只脚几乎并在一起', '新：前脚落在身体前下方，后腿还在身后'),
       ('下沉', .075, '旧：落地后身体不往下沉', '新：落地马上屈膝下沉，看得出重量'),
       ('踢腿', .125, '旧：后脚贴着地面往前拖', '新：后脚跟往上踢，小腿折起来'),
       ('蹬地', .34, '旧：后腿弯着，脚跟抬不起来', '新：后腿蹬直，脚尖蹬地，膝盖往前送'),
       ('腾空', .42, '旧：一直有脚在地上，没有腾空', '新：两只脚都离地，前腿往前够')]
CW, CH = 480, 520; TOP = 56; LAB = 40; NOTE = 64
W = CW * len(KEY) + 120; H = TOP + 2 * (CH + NOTE)
img = Image.new('RGB', (W, H), (255, 255, 255)); d = ImageDraw.Draw(img)
for j, (nm, u, a, b) in enumerate(KEY):
    x0 = 120 + j * CW; d.text((x0 + CW // 2 - 36, 8), nm, fill=(20, 20, 20), font=F(36))
    o = old.render(old.pose_run(u), ground_off=u * old.CYCLE).convert('RGB'); cell = Image.new('RGB', (CW, CH), (236, 233, 224)); cell.paste(o, (40, 0))
    img.paste(cell, (x0, TOP)); d.text((x0 + 12, TOP + CH + 8), a, fill=(170, 40, 40), font=F(22))
    n = new.render(new.pose(u)).convert('RGB'); img.paste(n, (x0, TOP + CH + NOTE)); d.text((x0 + 12, TOP + 2 * CH + NOTE + 8), b, fill=(20, 120, 60), font=F(22))
for k, (t, c) in enumerate((('上一版', (170, 40, 40)), ('重做', (20, 120, 60)))):
    y = TOP + k * (CH + NOTE) + CH // 2 - 40
    for i, ch in enumerate(t): d.text((40, y + i * 44), ch, fill=c, font=F(38))
for j in range(1, len(KEY)): d.line([(120 + j * CW, TOP), (120 + j * CW, H)], fill=(255, 255, 255), width=4)
img.save('out/对照_旧版vs重做_关键姿势.png'); print(img.size)
