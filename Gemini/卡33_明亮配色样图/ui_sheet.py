# 明亮配色样图：每个方向一张展示图（行动页 + 整备页 + 主色 + 说明）。
import os, sys
from PIL import Image, ImageDraw, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))   # 仓库根目录（本脚本在 Gemini/卡33_明亮配色样图/）
import ui_recolor as U
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 46); fH = ImageFont.truetype(F, 28); fS = ImageFont.truetype(R, 23)
DESC = {
 'A': ('糖果街机', '奶白 + 浅粉的砖墙，薄荷绿、柠檬黄点缀；界面：珊瑚红侧栏、薄荷青属性板、天蓝点点底。',
       ['气质：可爱、潮玩、扭蛋机，和「回收潮牌」最搭。', '品牌里投币帮、信号鬼放进去最自然。', '风险：粉色多了会偏甜，战斗特效要更亮才压得住。']),
 'B': ('晴天涂鸦', '正午阳光 + 街头涂鸦，饱和的天蓝、橙、黄、青；界面：宝蓝侧栏、番茄橙属性板、黄色斜纹底。',
       ['气质：街头、运动、有劲儿（Splatoon 那一路）。', '路锥俱乐部这种工地 / 街头品牌最搭。', '规矩：角色站的那块墙不画大涂鸦（这张已按这个规矩重出，涂鸦挪到两边）。']),
 'C': ('柠檬汽水', '柔和的柠檬黄、薄荷绿、淡天蓝，瓷砖墙、小彩旗、冒泡的水；界面：天蓝侧栏、葡萄紫属性板、薄荷气泡底。',
       ['气质：清爽、干净，适合长时间挂机看着不累。', '所有品牌都不打架。', '风险：颜色淡，画面容易「平」，要靠角色和特效撑饱和度。']),
}
COMMON = '界面字：亮底上白字看不清，关卡名、波次、血量数字改成深色字，底下垫半透明白底。'
def wrap(d, x, y, text, font, width, fill, lh):
    buf = ''
    for ch in text:
        if d.textlength(buf + ch, font=font) > width: d.text((x, y), buf, font=font, fill=fill); y += lh; buf = ''
        buf += ch
    d.text((x, y), buf, font=font, fill=fill); return y + lh
def sheet(k, dst):
    name, line, notes = DESC[k]; P = U.PALETTES[k]
    W, pad = 1920, 30
    act = Image.open(os.path.join(HERE, '出图', 'act_%s.png' % k)).resize((1280, 720), Image.LANCZOS)
    lo = Image.open(os.path.join(HERE, '出图', 'load_%s.png' % k)).resize((1280, 720), Image.LANCZOS)
    H = 150 + 720 + pad + 720 + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); d = ImageDraw.Draw(sh)
    d.text((pad, 28), '明亮配色方向 %s · %s' % (k, name), font=fT, fill=(30, 34, 60))
    d.text((pad, 96), line, font=fS, fill=(70, 74, 90))
    sh.paste(act, (pad, 150)); sh.paste(lo, (pad, 150 + 720 + pad))
    x0 = pad + 1280 + pad; y = 150; tw = W - x0 - pad
    d.text((x0, y), '行动页（实机角色 + 新背景）', font=fH, fill=(30, 34, 60)); y += 50
    for t in notes + [COMMON]: y = wrap(d, x0, y, t, fS, tw, (60, 64, 80), 36) + 10
    y += 14; d.text((x0, y), '主色', font=fH, fill=(30, 34, 60)); y += 50
    for i, (lab, c) in enumerate([('底', P['bg']), ('花纹', P['bg2']), ('侧栏', P['side']), ('属性板', P['stats']), ('屏', P['lcd'])]):
        cx = x0 + i * 106; rgb = ((c >> 16) & 255, (c >> 8) & 255, c & 255)
        d.rounded_rectangle((cx, y, cx + 90, y + 90), 14, fill=rgb, outline=(40, 40, 50), width=2)
        d.text((cx + 4, y + 98), lab, font=fS, fill=(60, 64, 80))
    y2 = 150 + 720 + pad
    d.text((x0, y2), '整备页（实机截图换色）', font=fH, fill=(30, 34, 60)); y2 += 50
    for t in ['深灰底、侧栏、属性板、战力屏换成新配色；卡片、角色、文字一个像素都没动。', '装备卡片保留深色：亮底上更显眼，品质色更好认。']:
        y2 = wrap(d, x0, y2, t, fS, tw, (60, 64, 80), 36) + 10
    sh.save(dst, quality=90); print(dst, sh.size)
if __name__ == '__main__':
    out = os.path.join(ROOT, 'Docs', '截图_2026-11-25'); os.makedirs(out, exist_ok=True)
    for k in 'ABC': sheet(k, os.path.join(out, '样图_明亮配色_%s%s.jpg' % (k, DESC[k][0])))
