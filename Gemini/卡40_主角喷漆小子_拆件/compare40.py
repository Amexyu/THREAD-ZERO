# 卡40：比例对比例图：现在 / 改后（本地拼）/ 设定图，同样高度，画上「头高」刻度线。
import sys, os, json, math, importlib.util, numpy as np
from PIL import Image, ImageDraw, ImageFont
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
ARGV = list(sys.argv)
def rig_img(D, anim='idle', t=0.0):
    spec = importlib.util.spec_from_file_location('pv', 'preview40.py')
    src = open('preview40.py', encoding='utf-8').read().split('frames = [')[0]   # 只要函数，不跑底下的拼图
    g = {'__name__': 'pv'}; sys.argv = ['x', D]; exec(src, g)
    g['CW'], g['CH'] = 360, 430
    return g['render'](anim, t)
def crop_fig(im, bgc):
    a = np.asarray(im.convert('RGB')).astype(int); diff = np.abs(a - np.array(bgc)).sum(2) > 30
    diff[-42:, :] = False; ys, xs = np.nonzero(diff); return im.crop((xs.min() - 10, ys.min() - 4, xs.max() + 10, ys.max() + 2))
now = crop_fig(rig_img('部件'), (236, 233, 224)); new = crop_fig(rig_img('部件v3'), (236, 233, 224)); walk = crop_fig(rig_img('部件v3', 'walk', .25), (236, 233, 224))
ref = Image.open(ARGV[1]).convert('RGBA'); bg = Image.new('RGBA', ref.size, (236, 233, 224, 255)); bg.alpha_composite(ref); ref = bg.crop(ref.getbbox())
HT = 760
def fit(im): return im.resize((round(im.width * HT / im.height), HT), Image.LANCZOS)
cols = [(fit(now), '现在（游戏里）'), (fit(new), '改后（本地拼的）'), (fit(walk), '改后 · 走路'), (fit(ref), '设定图（方案①）')]
heads = {0: None, 1: None, 2: None, 3: None}
W = sum(c.width for c, _ in cols) + 60 * (len(cols) + 1); S = Image.new('RGB', (W, HT + 210), (250, 248, 240)); d = ImageDraw.Draw(S)
d.text((30, 14), '比例对比：同样身高，红线 = 一个头高（帽顶到下巴）', fill=(20, 20, 20), font=ImageFont.truetype(F, 40))
x = 60; f = ImageFont.truetype(F, 30)
head_frac = [float(v) for v in ARGV[2].split(',')]   # 每列「帽顶到下巴」占全身的比例（手量）
for (im, lab), hf in zip(cols, head_frac):
    y0 = 120; S.paste(im.convert('RGB'), (x, y0)); hh = HT * hf
    k = 0
    while y0 + k * hh <= y0 + HT + 1:
        yy = y0 + k * hh; d.line([(x - 20, yy), (x + im.width + 20, yy)], fill=(230, 40, 40), width=3); k += 1
    d.text((x, y0 + HT + 20), lab + f'  约 {1 / hf:.1f} 头身', fill=(20, 20, 20), font=f)
    x += im.width + 60
S.save(ARGV[3], quality=88); print(S.size)
