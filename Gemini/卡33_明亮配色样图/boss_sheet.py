# 五章首领战样图总表：每章一行，左边「现在」（实机截图），右边「改成」（柠檬汽水背景 + 回收机器人首领）。
#   用法: python3 boss_sheet.py <现在 b1a_full.png… 所在目录> <改成 boss_1_C.jpg… 所在目录> <输出.jpg>
import os, sys
from PIL import Image, ImageDraw, ImageFont
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 44); fH = ImageFont.truetype(F, 28); fM = ImageFont.truetype(F, 22); fS = ImageFont.truetype(R, 22)
INK, SUB, MUTE = (30, 34, 60), (70, 74, 90), (120, 124, 140)
ROWS = [('第一章 · 运河旧街', '1-7 抽水总闸', '闸门看守', '售货机首领'),
        ('第二章 · 晴天高架', '2-7 总调度塔', '总调度吊臂', '抓娃娃机吊车首领'),
        ('第三章 · 信号塔天台', '3-7 监视穹顶', '断讯监视者', '监控摄像头首领'),
        ('第四章 · 镜面上城', '4-7 审计之庭', '镜面审计官', '收银机首领'),
        ('第五章 · 霓虹夜市（傍晚）', '5-7 零号核心', '织网母体', '缝纫机首领')]
NOTES = ['截图是首领关开打 3 秒（首领刚走过来，还没贴到主角身上）。背景用首领房间的分层原图让 Gemini 按「柠檬汽水」重画，没有涂抹。',
         '原版首领原地换成回收机器人首领：按原版在屏幕上的大小配，脚底对齐原版；血条、首领名字、伤害数字、主角都是实机抠出来的。',
         '首领名字和血量那一行原来是白字，亮底上读不清，这里垫了白底、换成深色字（和左上角关卡名一样的做法）。',
         '血条框还是现在的深色金属框（带橙色夹子）；整套换亮色的话，血条框也要跟着换浅色的。']
def wrap(dr, x, y, text, font, width, fill, lh):
    buf = ''
    for ch in text:
        if dr.textlength(buf + ch, font=font) > width: dr.text((x, y), buf, font=font, fill=fill); y += lh; buf = ''
        buf += ch
    dr.text((x, y), buf, font=font, fill=fill); return y + lh
def sheet(now_dir, new_dir, dst):
    tw, th, pad, lw = 800, 450, 24, 270
    W = pad + lw + 2 * (tw + 14) + pad - 14
    top = 176
    probe = ImageDraw.Draw(Image.new('RGB', (10, 10)))
    note_h = sum(((int(probe.textlength(t, font=fS)) // (W - 2 * pad)) + 1) * 34 + 10 for t in NOTES) + 60
    H = top + len(ROWS) * (th + 18) + note_h + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); dr = ImageDraw.Draw(sh)
    dr.text((pad, 22), '五章首领战 · 明亮配色样图（柠檬汽水）', font=fT, fill=INK)
    dr.text((pad, 84), '只是样图，游戏没改。左：现在的实机画面；右：亮色背景 + 回收机器人首领。', font=fS, fill=SUB)
    for c, t in enumerate(('现在', '改成')):
        dr.text((pad + lw + c * (tw + 14), top - 40), t, font=fH, fill=INK)
    for r, (chn, stage, old, new) in enumerate(ROWS):
        y = top + r * (th + 18)
        dr.text((pad, y + 6), chn.split(' · ')[0], font=fH, fill=INK)
        yy = wrap(dr, pad, y + 50, chn.split(' · ')[1], fM, lw - 20, SUB, 32)
        dr.text((pad, yy + 4), stage, font=fS, fill=MUTE)
        dr.text((pad, yy + 50), '首领', font=fS, fill=MUTE)
        yy = wrap(dr, pad, yy + 82, old, fM, lw - 20, SUB, 30)
        dr.text((pad, yy), '↓', font=fM, fill=MUTE)
        wrap(dr, pad, yy + 32, new, fM, lw - 20, (214, 92, 40), 30)
        a = Image.open(os.path.join(now_dir, 'b%da_full.png' % (r + 1))).convert('RGB').resize((tw, th), Image.LANCZOS)
        b = Image.open(os.path.join(new_dir, 'boss_%d_C.jpg' % (r + 1))).convert('RGB').resize((tw, th), Image.LANCZOS)
        sh.paste(a, (pad + lw, y)); sh.paste(b, (pad + lw + tw + 14, y))
    y = top + len(ROWS) * (th + 18) + 10
    dr.text((pad, y), '说明', font=fH, fill=INK); y += 46
    for t in NOTES: y = wrap(dr, pad, y, t, fS, W - 2 * pad, (60, 64, 80), 34) + 10
    sh.save(dst, quality=86); print(dst, sh.size)
if __name__ == '__main__':
    sheet(*sys.argv[1:4])
