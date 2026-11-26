# 卡47：套装样图（每套一行：配色 + 名字 + 主题 + 套装效果 | 帽子 / 上衣 / 背包 / 裤子 / 鞋）
import os, json, numpy as np, cv2
from PIL import Image, ImageDraw, ImageFont
FB = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc', s, index=2)
FR = lambda s: ImageFont.truetype('/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc', s, index=2)
SETS = [
 ('切图/套装3', 0, '回收社', 'RECYCLE CO.', '主角现在这一身（起始款）', [['没有套装效果']],
  ['老规矩', '颜料卫衣', '回收桶', '叠穿', '溅满了'], ['反戴鸭舌帽', '连帽卫衣', '铁皮垃圾桶', '短裤 + 打底裤', '高帮帆布鞋']),
 ('切图/套装1', 0, '雨天巡逻', 'RAIN PATROL', '下雨天沿着水渠巡逻', [['2 件：冲刺距离 +15%'], ['4 件：冲刺撞到的第一只会溅开，', '周围的一起挨刷']],
  ['反光条', '躲雨', '满满一桶', '踩水', '跳水坑'], ['渔夫帽', '雨衣', '饮水机水桶', '短裤', '高筒雨靴']),
 ('切图/套装1', 1, '屋顶菜园', 'ROOFTOP GARDEN', '在屋顶种菜的小园丁', [['2 件：回体力 +20%'], ['4 件：体力满的时候，', '多回的变成护盾']],
  ['发芽', '卷袖子', '浇浇水', '补丁背带', '踩泥'], ['草帽', '格子衬衫', '洒水壶', '背带短裤', '园艺洞洞鞋']),
 ('切图/套装1', 2, '码头搬运', 'DOCK RUNNERS', '码头搬货的年轻工人', [['2 件：生命 +10%'], ['4 件：每 8 秒第一次挨打，', '伤害减半']],
  ['上工', '海魂衫', '易碎勿压', '十个口袋', '钢头'], ['毛线帽', '条纹衫 + 反光背心', '小货柜箱', '工装裤', '工作靴']),
 ('切图/套装2', 0, '路锥俱乐部', 'CONE CLUB', '把路锥穿成潮牌的街头团体', [['2 件：击退 +20%'], ['4 件：被击退的会撞退', '身后那一只']],
  ['施工中', '看得见', '叠叠乐', '侧边杠', '厚底'], ['路锥头盔', '反光夹克', '路锥背包', '运动裤', '厚底鞋']),
 ('切图/套装2', 1, '信号鬼', 'SIGNAL GHOST', '在屋顶天线之间乱窜的小孩', [['2 件：技能冷却 −8%'], ['4 件：放完技能 1 秒内，', '闪开下一次攻击']],
  ['收不到', '电路图', '雪花台', '滚边', '紫底'], ['兔耳天线兜帽', '电路卫衣', '老电视', '束脚裤', '运动鞋']),
 ('切图/套装2', 2, '投币帮', 'COIN CREW', '泡在街机厅的一伙人', [['2 件：掉落 +10%'], ['4 件：每打倒 20 只，', '吐一颗扭蛋（零件或碎片）']],
  ['再来一局', '贴满了', '扭一个', '棋盘格', '投币鞋'], ['复古棒球帽', '棒球夹克', '扭蛋机', '格子短裤', '高帮鞋']),
 ('切图/套装3', 1, '晚霞上城', 'DUSK UPTOWN', '上城区的讲究打扮', [['2 件：暴击 +5%'], ['4 件：暴击会贴标记，', '被标记的多挨 15% 伤害']],
  ['看镜子', '紫藤', '一瓶晚霞', '阔腿', '垫高'], ['贝雷帽 + 护目镜', '长风衣', '香水瓶', '阔腿裤', '金色厚底鞋']),
 ('切图/套装3', 2, '彩线拼织', 'THREAD', '全城的旧毛线拼出来的（终局）', [['2 件：全属性 +3%'], ['4 件：彩线单品能当任何一套的', '散件，用来凑别的套装']],
  ['绒球', '百色', '毛线团', '补丁', '七色鞋带'], ['毛线帽', '拼织毛衣', '毛线篮', '补丁牛仔裤', '彩色鞋带鞋']),
]
SLOT = ['帽子', '上衣', '背包', '裤子', '鞋']
def palette(pieces, k=9, n=4):
    px = []
    for pc in pieces:
        a = np.asarray(pc.convert('RGBA')).reshape(-1, 4); a = a[a[:, 3] > 200][:, :3].astype(np.float32)
        lum = a.mean(1); sat = a.max(1) - a.min(1); a = a[(lum > 45) & ~((lum > 225) & (sat < 25))]   # 去掉描边黑和高光白
        if len(a) > 6000: a = a[np.random.RandomState(1).choice(len(a), 6000, replace=False)]
        px.append(a)
    X = np.concatenate(px)
    _, lab, cen = cv2.kmeans(X, k, None, (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 40, .5), 4, cv2.KMEANS_PP_CENTERS)
    cnt = np.bincount(lab.ravel(), minlength=k).astype(np.float32); sat = cen.max(1) - cen.min(1); score = cnt * (.35 + sat / 255); order = np.argsort(-score); out = []
    for i in order:
        c = cen[i]
        if all(np.abs(c - o).sum() > 90 for o in out): out.append(c)
        if len(out) == n: break
    return [tuple(int(v) for v in c) for c in out]
W = 2600; LW = 560; CW = (W - LW - 40) // 5; RH = 340; TOP = 160
img = Image.new('RGB', (W, TOP + RH * len(SETS) + 80), (246, 243, 236)); d = ImageDraw.Draw(img)
for y in range(0, img.height, 14):
    for x in range((y // 14 % 2) * 7, W, 14): d.ellipse([x - 1, y - 1, x + 1, y + 1], fill=(232, 228, 219))
d.text((40, 26), '套装 · 按主题设计（九套样图）', fill=(20, 20, 20), font=FB(48))
d.text((40, 96), '每套是一个完整的主题造型，自己配 3–4 个颜色，不按颜色分，也不跟章节绑。漆的颜色还是跟上色工具走，背包只是衣服的一部分。', fill=(70, 70, 70), font=FR(26))
for i, (folder, row, name, en, theme, bonus, nick, what) in enumerate(SETS):
    y0 = TOP + i * RH; x0 = 20
    d.rectangle([x0, y0 + 8, W - 20, y0 + RH - 12], fill=(255, 255, 255), outline=(20, 20, 20), width=5)
    meta = {p['row'] * 10 + p['col']: p for p in json.load(open(os.path.join(folder, 'pieces.json')))}
    pcs = [Image.open(os.path.join(folder, meta[row * 10 + c]['file'])).convert('RGBA') for c in range(5)]
    pal = palette(pcs)
    for k, c in enumerate(pal): d.rectangle([x0 + 26 + k * 46, y0 + 30, x0 + 64 + k * 46, y0 + 68], fill=c, outline=(20, 20, 20), width=3)
    sw_end = x0 + 26 + len(pal) * 46
    d.text((sw_end + 12, y0 + 20), name, fill=(20, 20, 20), font=FB(40))
    d.text((x0 + 28, y0 + 84), en, fill=(110, 110, 110), font=FB(22))
    d.text((x0 + 28, y0 + 120), theme, fill=(40, 40, 40), font=FB(25))
    for k, lines in enumerate(bonus):   # 每条效果手动断行
        for m, l2 in enumerate(lines): d.text((x0 + 28 + (0 if m == 0 else 66), y0 + 170 + k * 52 + m * 32), l2, fill=(70, 70, 70), font=FR(23))
    for c in range(5):
        pc = pcs[c]; bw, bh = CW - 60, RH - 112
        s = min(bw / pc.width, bh / pc.height, 1.0); pc = pc.resize((max(1, int(pc.width * s)), max(1, int(pc.height * s))), Image.LANCZOS)
        cx = LW + c * CW + CW // 2; img.paste(pc, (cx - pc.width // 2, y0 + 26 + (bh - pc.height) // 2), pc)
        t1 = '「%s」' % nick[c]; t2 = SLOT[c] + ' · ' + what[c]
        w1 = d.textlength(t1, font=FB(26)); w2 = d.textlength(t2, font=FR(20))
        d.text((cx - w1 / 2, y0 + RH - 84), t1, fill=(20, 20, 20), font=FB(26)); d.text((cx - w2 / 2, y0 + RH - 48), t2, fill=(100, 100, 100), font=FR(20))
d.text((40, img.height - 56), '左边色块是从这套单品里取的颜色。大字是昵称，小字是部位和东西。图是 Gemini 单品图（卡47）；套装效果是举例，数值等装备结构定了再调。', fill=(110, 110, 110), font=FR(22))
img.save('样图_套装九套_按主题.png'); img.convert('RGB').save('样图_套装九套_按主题.jpg', quality=90); print(img.size)
