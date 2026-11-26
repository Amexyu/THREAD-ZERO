import os, json
from PIL import Image, ImageDraw, ImageFont
FB = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', s, index=2)
FR = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc', s, index=2)
FAM = [
 ('切图/工具A', 0, '排笔', '短刃', '贴身连刷，四连击', ['老排笔', '缠胶带', '三连刷']),
 ('切图/工具A', 1, '喷漆壶', '枪', '远程连发漆弹', ['喷喷', '打气筒', '喷花机']),
 ('切图/工具A', 2, '泼漆桶', '霰弹', '近距离一泼一大片', ['一桶', '贴满贴纸', '摇摇桶']),
 ('切图/工具A', 3, '颜料气球弹弓', '手炮', '颜料气球砸中炸开一圈', ['弹弓', '双皮筋', '连发弹弓']),
 ('切图/工具B', 0, '印章锤', '重锤', '砸地盖一个大印', ['盖章', '齿轮章', '五角星章']),
 ('切图/工具B', 1, '颜料溜溜球', '钩索', '甩出去再收回来，一路刷一串', ['溜溜', '螺母溜溜', '齿轮溜溜']),
 ('切图/工具B', 2, '高压颜料水管', '线圈', '持续喷，越喷越浓', ['浇花', '打压', '消防栓']),
 ('切图/工具B', 3, '长柄滚筒', '长柄', '推着冲，地上留一道漆', ['刷墙', '带轮子', '笼子滚筒']),
]
TIER = ['基础款', '改装款', '高级款']
W = 2300; LW = 560; CW = (W - LW - 40) // 3; RH = 300; TOP = 150
img = Image.new('RGB', (W, TOP + RH * len(FAM) + 70), (246, 243, 236)); d = ImageDraw.Draw(img)
for y in range(0, img.height, 14):
    for x in range((y // 14 % 2) * 7, W, 14): d.ellipse([x - 1, y - 1, x + 1, y + 1], fill=(232, 228, 219))
d.text((40, 28), '上色工具 · 8 种 × 3 个型号（Gemini 单品图）', fill=(20, 20, 20), font=FB(46))
d.text((40, 92), '白色的部分是「漆」，游戏里按颜料桶的颜色染。工具决定怎么刷（沿用现有 8 种攻击动作），颜色由背包决定。', fill=(70, 70, 70), font=FR(26))
for i, (folder, row, name, anim, how, nick) in enumerate(FAM):
    y0 = TOP + i * RH; x0 = 20
    d.rectangle([x0, y0 + 8, W - 20, y0 + RH - 12], fill=(255, 255, 255), outline=(20, 20, 20), width=5)
    d.text((x0 + 26, y0 + 26), name, fill=(20, 20, 20), font=FB(42))
    d.text((x0 + 26, y0 + 88), '动作：沿用「%s」' % anim, fill=(90, 90, 90), font=FB(24))
    d.text((x0 + 26, y0 + 130), how, fill=(50, 50, 50), font=FR(24))
    meta = {p['row'] * 10 + p['col']: p for p in json.load(open(os.path.join(folder, 'pieces.json')))}
    for c in range(3):
        p = meta.get(row * 10 + c)
        if p is None: continue
        pc = Image.open(os.path.join(folder, p['file'])).convert('RGBA'); bw, bh = CW - 60, RH - 100
        s = min(bw / pc.width, bh / pc.height, 1.0); pc = pc.resize((max(1, int(pc.width * s)), max(1, int(pc.height * s))), Image.LANCZOS)
        cx = LW + c * CW + CW // 2; img.paste(pc, (cx - pc.width // 2, y0 + 22 + (bh - pc.height) // 2), pc)
        t = '「%s」 %s' % (nick[c], TIER[c]); tw = d.textlength(t, font=FB(26)); d.text((cx - tw / 2, y0 + RH - 66), t, fill=(30, 30, 30), font=FB(26))
d.text((40, img.height - 52), '个别贴纸上 Gemini 画了乱码字，正式出图时去掉。型号的数值区别（射程、范围、连击数）等装备结构定了再定。', fill=(110, 110, 110), font=FR(22))
img.save('样图_上色工具八种三型号.png'); img.convert('RGB').save('样图_上色工具八种三型号.jpg', quality=90); print(img.size)
