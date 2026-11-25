# 敌人样图展示图：现在 vs 改成（6 种）+ 放进 1-1 的两张样图。
import os, sys
from PIL import Image, ImageDraw, ImageFont
import numpy as np
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 44); fH = ImageFont.truetype(F, 24); fS = ImageFont.truetype(R, 22)
LAB_O = ['机械狗', '侦察无人机', '重型', '精英 · 粉碎者', '精英 · 信标', '首领 · 闸门看守']
LAB_N = ['扫地机器狗', '电饭锅无人机', '冰箱重型', '洗衣机精英', '旧电视信标', '售货机首领']
def trim(im):
    a = np.asarray(im)[..., 3]; ys, xs = np.where(a > 30); return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
def sheet(orig, new, act, lineup, dst):
    W, pad = 1920, 30; cw = (W - 2 * pad) // 6; chh = 250
    y1 = 150; y2 = y1 + chh + 110; y3 = y2 + chh + 110; H = y3 + 44 + 515 + 70
    sh = Image.new('RGB', (W, H), (250, 250, 247)); d = ImageDraw.Draw(sh)
    d.text((pad, 26), '敌人样图 · 离家出走的回收机器人（第一章）', font=fT, fill=(30, 34, 60))
    d.text((pad, 88), '轮廓、姿势、武器、朝向照原版，只换造型和配色：城市里的旧家电自己跑出来捣乱，可爱但有点坏。Gemini 一张图出 6 个，本地切开。', font=fS, fill=(70, 74, 90))
    def row(ims, labels, y, title):
        d.text((pad, y - 6), title, font=fH, fill=(110, 114, 130))
        for i, (im, lab) in enumerate(zip(ims, labels)):
            im = trim(im); k = min((cw - 30) / im.width, (chh - 20) / im.height); im = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)
            sh.paste(Image.new('RGB', (cw - 12, chh + 6), (238, 240, 244)), (pad + i * cw + 6, y + 28))
            sh.paste(im, (pad + i * cw + (cw - im.width) // 2, y + 30 + (chh - im.height)), im)
            d.text((pad + i * cw + 12, y + chh + 42), lab, font=fH, fill=(30, 34, 60))
    row(orig, LAB_O, y1, '现在'); row(new, LAB_N, y2, '改成')
    d.text((pad, y3), '放进 1-1（C 柠檬汽水）：左 = 第 3 波原地换成扫地机器狗；右 = 和主角同框，按游戏里的大小', font=fH, fill=(30, 34, 60))
    sh.paste(Image.open(act).convert('RGB').resize((915, 515), Image.LANCZOS), (pad, y3 + 44))
    sh.paste(Image.open(lineup).convert('RGB').resize((915, 515), Image.LANCZOS), (pad + 945, y3 + 44))
    d.text((pad, y3 + 44 + 515 + 18), '贴纸上的英文（原来有 ROOMBA 等字样，ROOMBA 是真实品牌名）已经用爱心、星星贴纸盖掉。还要做：每种还要出蓄力、出手两个姿势。', font=fS, fill=(70, 74, 90))
    sh.save(dst, quality=90); print(dst, sh.size)
if __name__ == '__main__':
    E, C, act, lineup, dst = sys.argv[1:6]
    names = ['swarm_idle', 'ranged_idle', 'brute_idle', 'elite_crusher_idle', 'elite_beacon_idle', 'boss_gatekeeper_idle']
    sheet([Image.open(os.path.join(E, n + '.png')).convert('RGBA') for n in names],
          [Image.open(os.path.join(C, 'cute_%d.png' % k)).convert('RGBA') for k in range(6)], act, lineup, dst)
