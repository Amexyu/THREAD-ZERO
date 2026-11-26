# 卡41：八种上色工具例图加标签（只做例图，不进游戏）
import sys
from PIL import Image, ImageDraw, ImageFont
src, dst = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGB'); W, H = im.size
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
top, bot = 96, 92
S = Image.new('RGB', (W, H + top + bot), (250, 248, 240)); S.paste(im, (0, top)); d = ImageDraw.Draw(S)
d.text((24, 18), '上色武器：8 种工具对上现有 8 种攻击动作（世界是黑白的，刷到哪里，哪里就有颜色）', fill=(20, 20, 20), font=ImageFont.truetype(F, 50))
labels = ['① 排笔 · 短刃：贴身连刷', '② 喷漆枪 · 枪：远程连发', '③ 泼漆桶 · 霰弹：一泼一大片', '④ 颜料气球炮 · 手炮：炸开一圈',
          '⑤ 印章锤 · 重锤：砸地盖大印', '⑥ 颜料溜溜球 · 钩索：甩出去刷一串', '⑦ 高压水管 · 线圈：持续喷', '⑧ 长柄滚筒 · 长柄：推着冲留一道漆']
f = ImageFont.truetype(F, 34); cw, ch = W / 4, H / 2
for i, t in enumerate(labels):
    x = int((i % 4) * cw) + 28; y = top + int((i // 4) * ch) + 22
    tw = d.textlength(t, font=f) + 24
    d.rectangle([x, y, x + tw, y + 50], fill=(255, 222, 40), outline=(0, 0, 0), width=4); d.text((x + 12, y + 3), t, fill=(0, 0, 0), font=f)
d.text((24, H + top + 18), '颜色 = 属性（程序染色，每件工具只画一次）：红 收尾 · 橙 无人机 · 黄 电弧 · 绿 汲取 · 蓝 推进 · 紫 标记 · 白 护盾', fill=(20, 20, 20), font=ImageFont.truetype(F, 44))
S.save(dst, quality=88); print(S.size)
