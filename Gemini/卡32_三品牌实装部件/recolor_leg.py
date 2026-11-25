# 下装：拿初始腿（近侧 / 远侧两张）按区域重新上色，外形、线稿、鞋的位置和游戏里完全一样（Gemini 一画腿就变长、变成两只脚）。
# 区域按亮度和高度分：裤子（上半，深色）、鞋面（浅色）、鞋上的深色细线（鞋带 / 绑带）、鞋底（最下面一截深色），外轮廓一圈保持深色。
# 用法：python3 recolor_leg.py <id> <款式>      款式：ghost / coin
import sys, os, json, numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))   # 仓库根目录（本脚本在 Gemini/卡32_三品牌实装部件/）
A = os.path.join(ROOT, 'ThreadZero/Assets/Resources/Art/V13/')
M = json.load(open(A + 'Starter/parts.json'))

def ramp(t, lo, hi):
    t = np.clip(t, 0, 1)[..., None]; return np.asarray(lo, float) * (1 - t) + np.asarray(hi, float) * t

def recolor(part, style, far=False):
    im = np.asarray(Image.open(A + 'Starter/' + part + '.png').convert('RGBA')).astype(float)
    rgb, al = im[..., :3], im[..., 3]; H, W = al.shape
    L = rgb.mean(-1); solid = al > 100
    yy = np.arange(H)[:, None] * np.ones((1, W))
    edge = solid & ~ndimage.binary_erosion(solid, iterations=3)
    PANTS_END, SOLE_START = 75, 113
    pants = solid & (yy < PANTS_END)
    boot = solid & (yy >= PANTS_END) & (yy < SOLE_START)
    sole = solid & (yy >= SOLE_START)
    line = (L < 30) | edge                       # 线稿：外轮廓 + 很深的线
    bootLine = boot & (L < 95) & ~edge           # 鞋面上的深色细线（绑带 / 鞋带）
    out = rgb.copy()
    tp = (L - 28) / (78 - 28)                    # 裤子布料的明暗
    tb = (L - 120) / (225 - 120)                 # 鞋面的明暗
    ts = (L - 25) / (110 - 25)                   # 鞋底
    if style == 'ghost':
        shorts = pants & (yy < 50); tights = pants & (yy >= 50)
        out[shorts & ~line] = ramp(tp, (46, 24, 70), (118, 70, 160))[shorts & ~line]
        out[tights & ~line] = ramp(tp, (20, 20, 26), (64, 64, 76))[tights & ~line]
        hem = pants & (np.abs(yy - 50) < 2) & ~edge; out[hem] = (22, 14, 30)        # 短裤下摆一道线
        out[boot & ~line & ~bootLine] = ramp(tb, (24, 24, 30), (78, 78, 92))[boot & ~line & ~bootLine]
        out[bootLine] = (120, 255, 70)                                               # 荧光绿鞋带
        out[sole & ~line] = ramp(ts, (14, 14, 18), (54, 54, 62))[sole & ~line]
    elif style == 'coin':
        out[pants & ~line] = ramp(tp, (120, 16, 24), (232, 52, 56))[pants & ~line]
        out[boot & ~line & ~bootLine] = ramp(tb, (170, 24, 32), (240, 70, 70))[boot & ~line & ~bootLine]
        out[bootLine] = (250, 250, 250)                                              # 白鞋带
        out[sole & ~line] = ramp(ts, (205, 205, 210), (255, 255, 255))[sole & ~line] # 白鞋底
    # 线稿稍微带一点底色，不是死黑
    out[line & solid] = out[line & solid] * 0.35 + np.asarray((18, 16, 22)) * 0.65
    img = Image.fromarray(np.dstack([np.clip(out, 0, 255), al]).astype(np.uint8), 'RGBA')
    d = ImageDraw.Draw(img)
    if style == 'ghost':   # 荧光绿抽绳：腰头前面（朝画面右）垂下两根
        cx = 86 if not far else 84
        for dx, ln in ((0, 18), (6, 14)):
            d.line([(cx + dx, 14), (cx + dx + 2, 14 + ln)], fill=(20, 30, 14, 255), width=5)
            d.line([(cx + dx, 14), (cx + dx + 2, 14 + ln)], fill=(130, 255, 80, 255), width=3)
        d.ellipse((cx - 4, 10, cx + 10, 18), outline=(130, 255, 80, 255), width=2)
    elif style == 'coin':  # 侧面一排白色小圆点：沿着裤腿靠画面左的轮廓往里一点
        for y in range(16, 72, 8):
            xs = np.nonzero(solid[y])[0]
            if len(xs) == 0: continue
            x = xs.min() + 9
            d.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(250, 250, 250, 255), outline=(60, 10, 14, 255))
    if far:   # 远侧腿暗一档
        a = img.getchannel('A'); rgb2 = Image.fromarray((np.asarray(img.convert('RGB')).astype(float) * 0.82).astype(np.uint8)); img = rgb2.convert('RGBA'); img.putalpha(a)
    return img

pid, style = sys.argv[1], sys.argv[2]
d = A + 'Parts/' + pid; os.makedirs(d, exist_ok=True)
j = {'scale': M['scale']}
for part, far in (('leg_near', False), ('leg_far', True)):
    recolor(part, style, far).save(d + '/' + part + '.png')
    j[part] = {'file': part + '.png', 'pivot': M['parts'][part]['pivot']}
json.dump(j, open(d + '/part.json', 'w'), indent=1); print(pid, style, j)
