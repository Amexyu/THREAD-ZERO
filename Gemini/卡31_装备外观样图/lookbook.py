"""装备外观样图：三个回收潮牌 + 跨品牌混搭（用 v0.29 皮肤排版）。"""
import sys, json
sys.path.insert(0, '/home/claude/mock')
import os
os.chdir('/home/claude/mock')
from skin import *
os.chdir('/home/claude/samples')

LW, LH = 1600, 2596
img = Image.new('RGBA', (P(LW), P(LH)), C(hx('26272B')))
# 背景：细网格 + 网点
grid = canvas(LW, LH); dg = ImageDraw.Draw(grid)
for gx in range(0, LW, 40): dg.line([P(gx), 0, P(gx), P(LH)], fill=(255, 255, 255, 10), width=P(1))
for gy in range(0, LH, 40): dg.line([0, P(gy), P(LW), P(gy)], fill=(255, 255, 255, 10), width=P(1))
img.alpha_composite(grid)
halftone(img, 0, LH - 700, 900, 700, (255, 255, 255, 14), step=13, rmax=2.8, origin='bl')
halftone(img, LW - 900, 0, 900, 600, (255, 255, 255, 12), step=13, rmax=2.8, origin='tr')

idx = json.load(open('cut/index.json'))
def piece(name, k): return Image.open(idx[name][k]['file']).convert('RGBA')

# ---------- 标题 ----------
plastic(img, 30, 26, LW - 60, 150, r=22)
text(img, 62, 44, '装备外观样图', 40, C(TXT), 'k')
spaced(img, 64, 100, 'GEAR LOOKBOOK · SALVAGE STREETWEAR', 10, C(TXT2), 'n', .4)
text(img, 64, 124, '套装松绑：一件装备只属于一个品牌，部位和件数不固定，跨品牌随便搭。收集按品牌累计，另有推荐穿搭图鉴。', 14, C(TXT2), 'b')
lcd(img, LW - 330, 52, 270, 44, 'v0.29 · SAMPLE', None, OR, 18)
for i, (lab, col) in enumerate((('回收潮改', OR), ('Q 版轮廓先行', hx('2E3034')), ('一件一个主角细节', hx('2E3034')))):
    tw0 = tw(lab, 12, 'k') + 24
    fill_round(img, LW - 330 + (0 if i == 0 else 0), 110 + 0, 0, 0, 0, col)
x0 = LW - 330
for i, (lab, col, tc) in enumerate((('回收潮改', OR, hx('FFFFFF')), ('轮廓先行', hx('2E3034'), hx('FFFFFF')), ('一件一个看点', hx('2E3034'), hx('FFFFFF')))):
    w0 = tw(lab, 11.5, 'k') + 20
    fill_round(img, x0, 112, w0, 26, 13, col); text(img, x0 + w0 / 2, 125, lab, 11.5, C(tc), 'k', 'mm'); x0 += w0 + 8

BRANDS = [
    dict(key='cone', en='CONE CLUB', zh='路锥俱乐部', code='CC', col=hx('FF6A1A'), sub=hx('D7FF3A'),
         palette=[hx('FF6A1A'), hx('F4F4F1'), hx('1C1D20'), hx('D7FF3A')],
         lines=['把路上回收来的路锥、路障、红绿灯', '改成潮牌。反光条 + 警示灯 + 黄黑斜纹，', '远远一眼就认得出来。'],
         items=[('别挡道', 'CC-01 · 路锥头盔', 1, 3), ('慢行', 'CC-02 · SLOW 反光夹克', 2, 2), ('施工中', 'CC-03 · 路障背包', 3, 4),
                ('减速带', 'CC-04 · 斜纹护膝短裤', 4, 1), ('红灯停', 'CC-05 · 信号灯霰枪', 5, 5)]),
    dict(key='ghost', en='GHOSTWAVE', zh='信号鬼', code='GW', col=hx('8B5CF6'), sub=hx('7CFF4F'),
         palette=[hx('7A4FD0'), hx('1B1B22'), hx('7CFF4F'), hx('B9BEC0')],
         lines=['旧电视、天线、收音机改装。', '紫黑配荧光绿，电视雪花和彩条测试图', '当印花；屏幕脸可以换表情。'],
         items=[('雪花屏', 'GW-01 · 显像管头盔', 1, 4), ('测试图', 'GW-02 · 彩条卫衣', 2, 3), ('满格', 'GW-03 · 天线背架', 3, 2),
                ('信号格', 'GW-04 · 工装短裤', 4, 4), ('调频', 'GW-05 · 收音机电磁炮', 5, 6)]),
    dict(key='coin', en='COIN-OP', zh='投币帮', code='CO', col=hx('E2363B'), sub=hx('F2C14E'),
         palette=[hx('E2363B'), hx('FFF1DF'), hx('2F6BD8'), hx('F2C14E')],
         lines=['自动贩卖机、易拉罐、扭蛋机改装。', '红白波普配蓝和金，背着一台', '会发光的迷你贩卖机。'],
         items=[('再来一个', 'CO-01 · 扭蛋头盔', 1, 2), ('冰镇', 'CO-02 · COLD DRINKS 夹克', 2, 4), ('补货中', 'CO-03 · 贩卖机背包', 3, 5),
                ('找零', 'CO-04 · 硬币运动裤', 4, 3), ('拉环', 'CO-05 · 汽水发射器', 5, 4)]),
]

def logo(im, x, y, label, col, size=34, angle=-4):
    w0 = tw(label, size, 'n') + 34; h0 = size + 22
    p = canvas(w0 + 10, h0 + 10)
    fill_round(p, 5, 7, w0, h0, 8, (0, 0, 0, 90))
    fill_round(p, 3, 3, w0 + 4, h0 + 4, 10, hx('FFFFFF'))
    fill_round(p, 5, 5, w0, h0, 8, col)
    otext(p, 5 + w0 / 2, 5 + h0 / 2, label, size, hx('FFFFFF'), stroke=INK2, sw=2.2, kind='n', anchor='mm')
    put(im, p, x, y, angle)
    return w0

y0 = 200
for bi, B in enumerate(BRANDS):
    ry = y0 + bi * 600
    # 左：品牌牌子
    plastic(img, 30, ry, 400, 560, r=22)
    logo(img, 52, ry + 26, B['en'], B['col'], 34, -3)
    text(img, 56, ry + 104, B['zh'], 30, C(TXT), 'k')
    spaced(img, 58, ry + 146, 'BRAND · ' + B['code'] + '-01 ~ ' + B['code'] + '-05', 9, C(TXT3), 'n', .35)
    for i, c in enumerate(B['palette']):
        fill_round(img, 56 + i * 52, ry + 172, 44, 44, 10, c); stroke_round(img, 56 + i * 52, ry + 172, 44, 44, 10, (0, 0, 0, 70), 1.2)
    for i, ln in enumerate(B['lines']): text(img, 56, ry + 236 + i * 26, ln, 14, C(TXT2), 'b')
    seam(img, 50, ry + 330, 410, ry + 330)
    text(img, 56, ry + 346, '品牌收集', 13, C(TXT), 'k')
    for i, (n, rew) in enumerate((('3 件', '核心 +1'), ('6 件', '织网碎片 +20'), ('全部', '称号「' + B['zh'] + '常客」'))):
        yy = ry + 374 + i * 40
        fill_round(img, 56, yy, 64, 30, 8, B['col'] if i == 2 else hx('2E3034')); text(img, 88, yy + 15, n, 13, C(hx('FFFFFF')), 'k', 'mm')
        text(img, 132, yy + 15, rew, 13, C(TXT), 'b', 'lm')
    text(img, 56, ry + 500, '件数不固定：这一季 5 件，后面的章节', 12, C(TXT3), 'r')
    text(img, 56, ry + 520, '还可以再出这个牌子的新款。', 12, C(TXT3), 'r')
    # 中：角色展示台
    cx0, cw0 = 446, 330
    plastic(img, cx0, ry, cw0, 560, r=22, top=hx('F3F1EC'), bottom=hx('D9D5CC'))
    recess(img, cx0 + 14, ry + 14, cw0 - 28, 532, r=14, top=hx('C8C3B8'), bottom=hx('E1DDD5'), depth=16)
    sp = canvas(cw0 - 28, 532); lglow(sp, (cw0 - 28) / 2, 120, 200, B['col'], .22, .6); lglow(sp, (cw0 - 28) / 2, 470, 120, hx('FFFFFF'), .5, .5)
    img.alpha_composite(sp, (P(cx0 + 14), P(ry + 14)))
    halftone(img, cx0 + 14, ry + 14, cw0 - 28, 532, (0, 0, 0, 22), step=10, rmax=2.2, origin='bl', reach=.9, clip_r=14)
    ImageDraw.Draw(img).ellipse(box(cx0 + cw0 / 2 - 100, ry + 486, 200, 36), fill=C(hx('2B2C30')))
    ImageDraw.Draw(img).ellipse(box(cx0 + cw0 / 2 - 90, ry + 490, 180, 28), outline=C(B['col']), width=P(2.4))
    ch = piece(B['key'], 0); hgt = 470; s = hgt / ch.size[1]
    if ch.size[0] * s > cw0 - 40: s = (cw0 - 40) / ch.size[0]
    paste(img, ch, cx0 + cw0 / 2 - ch.size[0] * s / 2, ry + 506 - ch.size[1] * s, ch.size[0] * s, ch.size[1] * s)
    tag_sticker(img, cx0 + 24, ry + 28, 96, 22, B['col'], '整套 · ' + B['code'], angle=-4, size=10.5, tc=hx('FFFFFF'))
    # 右：五张卡
    kx = 796
    for i, (nick, model, k, q) in enumerate(B['items']):
        slot = [1, 2, 3, 4, 0][i]
        x = kx + i * 158
        gear_card(img, x, ry + 40, 146, 300, piece(B['key'], k), q, nick, None, None, None, slot,
                  sub=model, art_ratio=.62, name_size=17, foot=B['en'])
    # 卡下面的说明条
    fill_round(img, kx, ry + 372, 776, 176, 16, (255, 255, 255, 10)); stroke_round(img, kx, ry + 372, 776, 176, 16, (255, 255, 255, 30), 1)
    notes = {'cone': ['头盔顶上的警示灯随攻击节奏闪；背包的小路锥走路时会晃。', '量产款是素色路锥，原型 / 零号加发光条和浮游路锥。'],
             'ghost': ['屏幕脸：待机笑脸、攻击时变成 >_<、受伤时雪花屏。', '武器打出去是荧光绿声波；零号款天线会浮起来。'],
             'coin': ['扭蛋头盔里看得到角色的头发；背包展示窗里的饮料会轮换。', '汽水发射器打出去冒气泡，暴击时喷一大团泡沫。']}[B['key']]
    text(img, kx + 22, ry + 392, '动起来的样子（实装时做）', 13, C(hx('FFFFFF')), 'k')
    for i, n in enumerate(notes):
        fill_round(img, kx + 22, ry + 432 + i * 34, 6, 6, 3, B['col'])
        text(img, kx + 38, ry + 435 + i * 34, n, 13, C(hx('D9DBDD')), 'r', 'lm')
    text(img, kx + 22, ry + 510, '名字：大字是昵称，小字是品牌型号和是什么东西。品质色照旧在卡边上。', 12, C(hx('9EA2A8')), 'r')

# ---------- 混搭 ----------
my = y0 + 3 * 600
plastic(img, 30, my, LW - 60, 560, r=22)
text(img, 62, my + 22, '跨品牌混搭', 30, C(TXT), 'k')
spaced(img, 64, my + 64, 'MIX & MATCH', 10, C(TXT2), 'n', .4)
text(img, 62, my + 92, '没有「必须穿整套」：任何一件都能和别的牌子搭。整套穿只是一种玩法，推荐穿搭会收进图鉴。', 14, C(TXT2), 'b')
recess(img, 46, my + 130, 860, 412, r=14, top=hx('C8C3B8'), bottom=hx('E1DDD5'), depth=16)
halftone(img, 46, my + 130, 860, 412, (0, 0, 0, 22), step=10, rmax=2.2, origin='br', reach=.9, clip_r=14)
for k, cxm in ((0, 46 + 215), (1, 46 + 645)):
    ch = piece('mix', k); hgt = 380; s = hgt / ch.size[1]
    ImageDraw.Draw(img).ellipse(box(cxm - 90, my + 500, 180, 30), fill=C(hx('2B2C30')))
    paste(img, ch, cxm - ch.size[0] * s / 2, my + 520 - ch.size[1] * s, ch.size[0] * s, ch.size[1] * s)
mixes = [('搭配 A「下班别找我」', [('头', '别挡道', 'CC'), ('衣', '冰镇', 'CO'), ('背', '满格', 'GW'), ('腿', '信号格', 'GW'), ('武', '红灯停', 'CC')]),
         ('搭配 B「夜市巡逻」', [('头', '雪花屏', 'GW'), ('衣', '慢行', 'CC'), ('背', '满格', 'GW'), ('腿', '找零', 'CO'), ('武', '拉环', 'CO')])]
bcol = {'CC': hx('FF6A1A'), 'GW': hx('8B5CF6'), 'CO': hx('E2363B')}
for mi, (title, parts) in enumerate(mixes):
    bx, by = 930, my + 136 + mi * 206
    lshadow(img, bx, by, 620, 190, 8, blur=6, alpha=.25, dy=4)
    fill_round(img, bx, by, 620, 190, 10, hx('FFFFFF'), hx('F3F1EB'))
    fill_round(img, bx + 18, by + 18, 5, 26, 2.5, OR)
    text(img, bx + 32, by + 16, title, 18, C(TXT), 'k')
    for i, (sl, nick, br) in enumerate(parts):
        px = bx + 18 + i * 120; py = by + 64
        fill_round(img, px, py, 112, 96, 10, hx('F4F2EC'), hx('E4E0D8')); stroke_round(img, px, py, 112, 96, 10, hx('CFCAC0'), 1.2)
        text(img, px + 12, py + 12, sl, 12, C(TXT3), 'k')
        text(img, px + 12, py + 34, nick, 16, C(TXT), 'k')
        w0 = tw(br, 10, 'n') + 16
        fill_round(img, px + 12, py + 64, w0, 20, 10, bcol[br]); text(img, px + 12 + w0 / 2, py + 74, br, 10, C(hx('FFFFFF')), 'n', 'mm')
stamp(img, 1460, my + 60, '推荐穿搭', OR, -10, 16)

out = img.convert('RGB').resize((1920, int(1920 * LH / LW)), Image.LANCZOS)
out.save('/mnt/user-data/outputs/样图_装备外观_回收潮牌.jpg', quality=92)
print(out.size)
