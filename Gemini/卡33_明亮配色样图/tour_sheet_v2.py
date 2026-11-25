# 五章一览：每个配色方向一张（1-1 … 5-1 的行动页样图），再加一张三个方向逐章对比。
import os, sys
from PIL import Image, ImageDraw, ImageFont
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 44); fH = ImageFont.truetype(F, 26); fS = ImageFont.truetype(R, 22)
CH = [('第一章 · 运河旧街', '1-1 排污口'), ('第二章 · 晴天高架', '2-1 货运坡道'), ('第三章 · 信号塔天台', '3-1 中继屋顶'),
      ('第四章 · 镜面上城', '4-1 镜面街口'), ('第五章 · 霓虹夜市（傍晚）', '5-1 织网边缘')]
DIR = {'A': '糖果街机', 'B': '晴天涂鸦', 'C': '柠檬汽水'}
def act(ch, d, base):
    return os.path.join(base, 'act_%s.png' % d) if ch == 1 else os.path.join(base, 'tour', 'act_%d_%s.png' % (ch, d))
def per_direction(d, base, dst):
    W, pad, tw, th = 1920, 30, 915, 515
    H = 130 + 3 * (th + 60) + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); dr = ImageDraw.Draw(sh)
    dr.text((pad, 26), '明亮配色方向 %s · %s · 五章一览（第二版）' % (d, DIR[d]), font=fT, fill=(30, 34, 60))
    dr.text((pad, 86), '第二版：第 2–5 章的底图改用游戏里的分层原图直接拼（不再是截图去掉角色），重画后没有涂抹；角色、怪、血条还是实机抠出来贴上去的。', font=fS, fill=(70, 74, 90))
    for i, (name, stage) in enumerate(CH):
        col, row = i % 2, i // 2
        x = pad + col * (tw + 30); y = 130 + row * (th + 60)
        im = Image.open(act(i + 1, d, base)).convert('RGB').resize((tw, th), Image.LANCZOS)
        sh.paste(im, (x, y + 40)); dr.text((x, y + 4), name, font=fH, fill=(30, 34, 60))
        dr.text((x + dr.textlength(name, font=fH) + 16, y + 8), stage, font=fS, fill=(110, 114, 130))
    # 第 6 格：说明
    x = pad + tw + 30; y = 130 + 2 * (th + 60) + 40
    notes = {'A': ['奶油、浅粉、薄荷：五章都很「甜」，章节之间靠建筑区分。', '第五章傍晚也压得住，彩灯和甜点贴纸最出彩。', '风险：五章都是粉色，看久了腻；可以每章换个主色（粉→薄荷→柠檬→天蓝→晚霞）。'],
             'B': ['饱和、对比强，街头感最足；最像「回收潮牌」的广告。', '第二章、第五章步道下层出图偏暗，各让 Gemini 提亮过一次；第二章下层还是偏深。', '涂鸦都避开了角色站位（提示词里写明了范围）。'],
             'C': ['最清爽、最耐看，长时间挂机不累眼。', '五章色调很统一，像一个系列。', '风险：颜色淡，角色和特效要更饱和才压得住（现在的主角红衣服刚好够）。']}[d]
    dr.text((x, y), '看下来', font=fH, fill=(30, 34, 60)); y += 46
    for t in notes:
        buf = ''
        for chh in t:
            if dr.textlength(buf + chh, font=fS) > tw: dr.text((x, y), buf, font=fS, fill=(60, 64, 80)); y += 34; buf = ''
            buf += chh
        dr.text((x, y), buf, font=fS, fill=(60, 64, 80)); y += 46
    sh.save(dst, quality=88); print(dst, sh.size)
def compare(base, dst):
    tw, th, pad = 620, 349, 24
    rows = ([('标题画面', '', '', 'title')] if os.path.exists(os.path.join(base, 'title_A.jpg')) else []) + \
           [(name.split(' · ')[0], name.split(' · ')[1], stage, r + 1) for r, (name, stage) in enumerate(CH)]
    W = 230 + 3 * (tw + 12) + pad; H = 126 + len(rows) * (th + 12) + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); dr = ImageDraw.Draw(sh)
    dr.text((pad, 20), '三个亮色方向逐章对比（第二版）', font=fT, fill=(30, 34, 60))
    for c, d in enumerate('ABC'):
        dr.text((230 + c * (tw + 12), 80), '%s · %s' % (d, DIR[d]), font=fH, fill=(30, 34, 60))
    for r, (t1, t2, t3, which) in enumerate(rows):
        y = 126 + r * (th + 12)
        dr.text((pad, y + 10), t1, font=fH, fill=(30, 34, 60))
        if t2: dr.text((pad, y + 48), t2, font=fS, fill=(70, 74, 90))
        if t3: dr.text((pad, y + 82), t3, font=fS, fill=(120, 124, 140))
        for c, d in enumerate('ABC'):
            f = os.path.join(base, 'title_%s.jpg' % d) if which == 'title' else act(which, d, base)
            sh.paste(Image.open(f).convert('RGB').resize((tw, th), Image.LANCZOS), (230 + c * (tw + 12), y))
    sh.save(dst, quality=86); print(dst, sh.size)

def title_sheet(base, dst):
    tw, th, pad = 930, 523, 30
    W = pad * 3 + tw * 2; H = 120 + 2 * (th + 56) + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); dr = ImageDraw.Draw(sh)
    dr.text((pad, 24), '标题画面 · 三个亮色方向', font=fT, fill=(30, 34, 60))
    dr.text((pad, 82), 'Gemini 按现在的标题插画重画（构图、断轨、高塔、主角站位不变）；标志和菜单字是游戏里的，换成深色字，左边垫一层浅色渐变。', font=fS, fill=(70, 74, 90))
    cells = [('现在（实机截图）', 'title_now.png')] + [('%s · %s' % (d, DIR[d]), 'title_%s.jpg' % d) for d in 'ABC']
    for i, (lab, f) in enumerate(cells):
        x = pad + (i % 2) * (tw + pad); y = 120 + (i // 2) * (th + 56)
        dr.text((x, y + 6), lab, font=fH, fill=(30, 34, 60))
        sh.paste(Image.open(os.path.join(base, f)).convert('RGB').resize((tw, th), Image.LANCZOS), (x, y + 46))
    sh.save(dst, quality=88); print(dst, sh.size)

if __name__ == '__main__':
    base = sys.argv[1] if len(sys.argv) > 1 else '.'
    out = sys.argv[2] if len(sys.argv) > 2 else '.'
    os.makedirs(out, exist_ok=True)
    for d in 'ABC': per_direction(d, base, os.path.join(out, '五章一览_%s%s.jpg' % (d, DIR[d])))
    compare(base, os.path.join(out, '三个方向逐章对比.jpg'))
    if os.path.exists(os.path.join(base, 'title_A.jpg')): title_sheet(base, os.path.join(out, '标题画面_三个方向.jpg'))
