# 第二～五章敌人样图展示图：现在 vs 改成（6 种）+ 放进 x-1 的两张样图（左：实机刚开打那一刻原地换；右：和主角同框按游戏大小站一排）。
#   用法: python3 enemy_sheet2.py <章 2~5> <原版 idle 目录> <cute 目录> <亮色背景> <实机前景(带主角)> <输出.jpg>
import os, sys, json, numpy as np, cv2
from PIL import Image, ImageDraw, ImageFont, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import wave_mock as WM
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 44); fH = ImageFont.truetype(F, 24); fS = ImageFont.truetype(R, 22)
CH = {
    '2': dict(title='第二章 · 物流高架', stage='2-1 货运坡道',
              old=['吊箱无人机', '小坦克', '叉车机器人', '精英 · 滚筒臂', '精英 · 运输机', '首领 · 总调度吊臂'],
              new=['外卖保温箱无人机', '行李箱坦克', '购物车叉车', '快递柜精英', '吊扇运输机', '抓娃娃机吊车首领'],
              note='保温箱无人机下面吊的礼物盒、娃娃机首领抓着的礼物盒，正好可以做成「掉落物」的样子；运输机的旋翼是吊扇叶片，转起来就是现成的动画。'),
    '3': dict(title='第三章 · 信号禁区', stage='3-1 中继屋顶',
              old=['蚊子无人机', '雷达车', '网盾重型', '精英 · 双盾', '精英 · 中继', '首领 · 断讯监视者'],
              new=['小风扇蜻蜓', '卫星锅遥控车', '空调外机重型', '热水器双盾精英', '路由器信标', '监控摄像头首领'],
              note='空调外机重型没有腿（就是一台外机顶着小屏幕），正式做走路动画时要给它加小轮子或小脚；卫星锅上原来有一张写着乱码的贴纸，已经用爱心贴纸盖掉。'),
    '4': dict(title='第四章 · 镜面上城', stage='4-1 镜面街口',
              old=['玻璃飞弹', '棱镜炮塔', '镜锤重型', '精英 · 镜盾', '精英 · 水晶柱', '首领 · 镜面审计官'],
              new=['粉饼盒飞弹', '落地台灯炮塔', '玻璃展柜重型', '衣柜穿衣镜精英', '香水瓶信标', '收银机首领'],
              note='玻璃展柜、香水瓶是透明的，背景会透过去（亮底上没问题）；收银机首领头顶那圈金币可以做成被打时往外掉金币。'),
    '5': dict(title='第五章 · 零号织网', stage='5-1 织网边缘',
              old=['线缆虫', '多管炮塔', '重型', '精英 · 利爪', '精英 · 协议球', '首领 · 织网母体'],
              new=['灯笼毛毛虫', '爆米花机炮塔', '火锅推车重型', '毛衣精英', '霓虹灯泡球信标', '缝纫机首领'],
              note='夜市背景里也挂着彩色灯笼，灯笼毛毛虫个头又小，容易和背景混在一起：正式做时要加粗描边或把灯笼换成和背景不撞的颜色。爆米花桶上原来有一处乱码字，已经用星星贴纸盖掉。'),
}
def trim(im):
    a = np.asarray(im)[..., 3]; ys, xs = np.where(a > 30); return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
def hero_of(fg_path):
    fg = Image.open(fg_path).convert('RGBA'); a = np.asarray(fg)
    m = (a[..., 3] > 60).astype(np.uint8); n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    i = max(range(1, n), key=lambda i: st[i][4] if 560 < st[i][0] < 760 and st[i][1] > 480 and st[i][3] > 250 else 0)
    x, y, w, h = st[i][:4]
    return Image.fromarray(np.where((lab == i)[..., None], a, 0).astype(np.uint8), 'RGBA').crop((x, y, x + w, y + h))
def lineup(ch, bg_path, hero_fg, cute_dir, dst, ground=822):
    bg = Image.open(bg_path).convert('RGB').resize((1920, 1080), Image.LANCZOS).convert('RGBA')
    items = [(hero_of(hero_fg), 0)]
    names = WM.NAMES[ch]
    for k in range(6):
        s = WM.fit(Image.open(os.path.join(cute_dir, 'cute_%d.png' % k)).convert('RGBA'), names[k])
        items.append((s, WM.INFO[names[k]]['lift']))
    total = sum(i.width for i, _ in items); gap = (1920 - 80 - total) / (len(items) - 1)
    xx = 40
    for im, lift in items:
        sh = Image.new('RGBA', bg.size, (0, 0, 0, 0)); d = ImageDraw.Draw(sh)
        d.ellipse((xx + im.width * .15, ground - 9, xx + im.width * .85, ground + 9), fill=(40, 40, 70, 60))
        bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(4)))
        bg.alpha_composite(im, (int(xx), int(ground - lift - im.height))); xx += im.width + gap
    bg.convert('RGB').save(dst, quality=92); print(dst)
def sheet(ch, E, C, act, lu, dst):
    cfg = CH[ch]; names = WM.NAMES[ch]
    orig = [Image.open(os.path.join(E, n + '_idle.png')).convert('RGBA') for n in names]
    new = [Image.open(os.path.join(C, 'cute_%d.png' % k)).convert('RGBA') for k in range(6)]
    W, pad = 1920, 30; cw = (W - 2 * pad) // 6; chh = 250
    y1 = 150; y2 = y1 + chh + 110; y3 = y2 + chh + 110; H = y3 + 44 + 515 + 100
    sh = Image.new('RGB', (W, H), (250, 250, 247)); d = ImageDraw.Draw(sh)
    d.text((pad, 26), '敌人样图 · 离家出走的回收机器人（%s）' % cfg['title'], font=fT, fill=(30, 34, 60))
    d.text((pad, 88), '和第一章同一个做法：轮廓、姿势、武器、朝向照原版，只换造型和配色（城市里的旧家电、日用品自己跑出来捣乱）。Gemini 一张图出 6 个，本地切开。', font=fS, fill=(70, 74, 90))
    def row(ims, labels, y, title):
        d.text((pad, y - 6), title, font=fH, fill=(110, 114, 130))
        for i, (im, lab) in enumerate(zip(ims, labels)):
            im = trim(im); k = min((cw - 30) / im.width, (chh - 20) / im.height); im = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)
            sh.paste(Image.new('RGB', (cw - 12, chh + 6), (238, 240, 244)), (pad + i * cw + 6, y + 28))
            sh.paste(im, (pad + i * cw + (cw - im.width) // 2, y + 30 + (chh - im.height)), im)
            d.text((pad + i * cw + 12, y + chh + 42), lab, font=fH, fill=(30, 34, 60))
    row(orig, cfg['old'], y1, '现在'); row(new, cfg['new'], y2, '改成')
    d.text((pad, y3), '放进 %s（C 柠檬汽水）：左 = 实机刚开打那一刻，怪原地换成新造型（血条、锁定框、伤害数字都是实机的）；右 = 和主角同框，按游戏里的大小' % cfg['stage'], font=fH, fill=(30, 34, 60))
    sh.paste(Image.open(act).convert('RGB').resize((915, 515), Image.LANCZOS), (pad, y3 + 44))
    sh.paste(Image.open(lu).convert('RGB').resize((915, 515), Image.LANCZOS), (pad + 945, y3 + 44))
    # 备注可能比一行长：按宽度折行
    words = cfg['note']; lines = []; cur = ''
    for chx in words:
        if d.textlength(cur + chx, font=fS) > W - 2 * pad: lines.append(cur); cur = chx
        else: cur += chx
    lines.append(cur)
    for j, ln in enumerate(lines): d.text((pad, y3 + 44 + 515 + 16 + j * 32), ln, font=fS, fill=(70, 74, 90))
    sh.save(dst, quality=90); print(dst, sh.size)
if __name__ == '__main__':
    ch, E, C, bg, hero_fg, dst = sys.argv[1:7]
    act = os.path.join(C, 'act_wave_C.jpg'); lu = os.path.join(C, 'lineup_C.jpg')
    lineup(ch, bg, hero_fg, C, lu)
    sheet(ch, E, C, act, lu, dst)
