# 卡45：品牌穿搭样图（每个品牌一行：品牌说明 + 帽子 / 上衣 / 颜料桶背包 / 裤子 / 鞋）
import os, json, numpy as np
from PIL import Image, ImageDraw, ImageFont
FB = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', s, index=2)
FR = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc', s, index=2)
BR = [
 ('切图/品牌1', 0, '回收社', 'RECYCLE CO.', '起始 · 褪色款', (150, 150, 150), '红 + 白（收尾 / 底漆）', '量产掉落都是这一身：灰的，只沾了几点红白漆',
  ['老规矩', '工号零', '先装着', '耐磨', '旧帆布'], ['反戴鸭舌帽', '连体工装', '铁皮垃圾桶', '工装裤', '帆布鞋']),
 ('切图/品牌1', 1, '水花', 'SPLASH', '第一章 · 沉降区', (60, 140, 230), '蓝（推进：像浪一样冲过去）', '穿 2 件：冲刺距离 +15%；穿 4 件：冲过去的路上留一道蓝漆',
  ['憋气', '躲雨', '满桶蓝', '冲浪', '踩水'], ['潜水镜棒球帽', '半透明雨衣', '饮水机水桶', '条纹短裤', '高筒雨靴']),
 ('切图/品牌1', 2, '太阳雨货柜', 'SUNSHOWER CARGO', '第二章 · 物流高架', (240, 190, 30), '黄（电弧：每第三下溅开）', '穿 2 件：溅开范围 +20%；穿 4 件：溅到的机器人也会再溅一次',
  ['上工', '易碎勿压', '一箱阳光', '多口袋', '赶车'], ['安全帽', '贴满运单的雨披', '小货柜箱', '工装短裤', '胶底跑鞋']),
 ('切图/品牌2', 0, '天线菜园', 'ROOFTOP GARDEN', '第三章 · 信号屋顶', (80, 170, 80), '绿（汲取：刷上去回体力）', '穿 2 件：回体力 +20%；穿 4 件：体力满时多出来的变成护盾',
  ['发芽', '十个口袋', '浇浇水', '背带', '沾泥'], ['插叶子的渔夫帽', '园丁背心', '洒水壶', '背带短裤', '帆布鞋']),
 ('切图/品牌2', 1, '晚霞镜面', 'DUSK MIRROR', '第四章 · 镜面上城', (150, 90, 200), '紫（标记：被刷紫的多挨刷）', '穿 2 件：标记加伤 +15%；穿 4 件：标记会传到旁边一只',
  ['看镜子', '紫藤', '一瓶晚霞', '宽一点', '垫高'], ['贝雷帽 + 镜面护目镜', '紫藤风衣', '香水瓶', '宽松裤', '厚底鞋']),
 ('切图/品牌2', 2, '彩线', 'THREAD', '第五章 · 零号织网', None, '彩虹（轮流触发所有颜色）', '穿 2 件：每种颜色都 +5%；穿 4 件：彩虹漆每轮多触发一种',
  ['绒球', '百色', '扭一个', '补丁', '七色鞋带'], ['毛线帽', '彩线拼织卫衣', '扭蛋机', '补丁牛仔裤', '彩色鞋带跑鞋']),
]
SLOT = ['帽子', '上衣', '颜料桶（背包）', '裤子', '鞋']
W = 2600; LW = 520; CW = (W - LW - 40) // 5; RH = 380; TOP = 150
img = Image.new('RGB', (W, TOP + RH * len(BR) + 70), (246, 243, 236)); d = ImageDraw.Draw(img)
# 网点底纹
for y in range(0, img.height, 14):
    for x in range((y // 14 % 2) * 7, W, 14): d.ellipse([x - 1, y - 1, x + 1, y + 1], fill=(232, 228, 219))
d.text((40, 28), '装备外观与种类 · 样图（Gemini 单品图，按品牌排）', fill=(20, 20, 20), font=FB(46))
d.text((40, 92), '规则：颜料桶背包决定漆的颜色（= 效果）；帽子、上衣、裤子、鞋决定风格和小加成。品质越高颜色越多：量产是褪色的灰，零号的漆会流动。', fill=(70, 70, 70), font=FR(26))
for i, (folder, row, name, en, chap, col, paint, bonus, nick, what) in enumerate(BR):
    y0 = TOP + i * RH; x0 = 20
    d.rectangle([x0, y0 + 8, W - 20, y0 + RH - 12], fill=(255, 255, 255), outline=(20, 20, 20), width=5)
    # 品牌说明
    if col is None:
        for k, c in enumerate([(230, 70, 60), (245, 150, 40), (240, 210, 40), (80, 180, 90), (60, 140, 230), (150, 90, 200)]): d.rectangle([x0 + 24 + k * 14, y0 + 30, x0 + 37 + k * 14, y0 + 70], fill=c)
        sw_end = x0 + 24 + 6 * 14
    else:
        d.rectangle([x0 + 24, y0 + 30, x0 + 104, y0 + 70], fill=col, outline=(20, 20, 20), width=3); sw_end = x0 + 104
    d.text((sw_end + 16, y0 + 22), name, fill=(20, 20, 20), font=FB(42))
    d.text((x0 + 26, y0 + 82), en + '　' + chap, fill=(90, 90, 90), font=FB(22))
    d.text((x0 + 26, y0 + 124), '漆：' + paint, fill=(30, 30, 30), font=FB(23))
    # 套装说明分两行
    words = bonus; lines = []
    while words:
        lines.append(words[:19]); words = words[19:]
    for k, ln in enumerate(lines[:4]): d.text((x0 + 26, y0 + 168 + k * 34), ln, fill=(60, 60, 60), font=FR(23))
    meta = {p['row'] * 10 + p['col']: p for p in json.load(open(os.path.join(folder, 'pieces.json')))}
    for c in range(5):
        p = meta.get(row * 10 + c)
        if p is None: continue
        pc = Image.open(os.path.join(folder, p['file'])).convert('RGBA'); bw, bh = CW - 50, RH - 120
        s = min(bw / pc.width, bh / pc.height, 1.0); pc = pc.resize((max(1, int(pc.width * s)), max(1, int(pc.height * s))), Image.LANCZOS)
        cx = LW + c * CW + CW // 2; img.paste(pc, (cx - pc.width // 2, y0 + 30 + (bh - pc.height) // 2), pc)
        t1 = '「%s」' % nick[c]; t2 = SLOT[c] + ' · ' + what[c]
        w1 = d.textlength(t1, font=FB(28)); w2 = d.textlength(t2, font=FR(20))
        d.text((cx - w1 / 2, y0 + RH - 92), t1, fill=(20, 20, 20), font=FB(28)); d.text((cx - w2 / 2, y0 + RH - 52), t2, fill=(100, 100, 100), font=FR(20))
d.text((40, img.height - 52), '大字是昵称，小字是部位和东西。图是 Gemini 单品图（卡45），只看外观和种类；数值是举例，等结构定了再调。', fill=(110, 110, 110), font=FR(22))
img.save('样图_品牌穿搭六种.png'); img.convert('RGB').save('样图_品牌穿搭六种.jpg', quality=90); print(img.size)
