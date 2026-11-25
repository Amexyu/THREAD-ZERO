# 五章首领战 · 三个亮色方向逐章对比：每章一行，现在 / A 糖果街机 / B 晴天涂鸦 / C 柠檬汽水。
#   用法: python3 boss_compare.py <现在 b1a_full.png… 所在目录> <boss_N_X.jpg 所在目录> <输出.jpg>
import os, sys
from PIL import Image, ImageDraw, ImageFont
F = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'; R = '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'
fT = ImageFont.truetype(F, 44); fH = ImageFont.truetype(F, 26); fM = ImageFont.truetype(F, 22); fS = ImageFont.truetype(R, 22)
INK, SUB, MUTE = (30, 34, 60), (70, 74, 90), (120, 124, 140)
ROWS = [('第一章', '运河旧街', '1-7 抽水总闸', '售货机首领'),
        ('第二章', '晴天高架', '2-7 总调度塔', '抓娃娃机吊车首领'),
        ('第三章', '信号塔天台', '3-7 监视穹顶', '监控摄像头首领'),
        ('第四章', '镜面上城', '4-7 审计之庭', '收银机首领'),
        ('第五章', '霓虹夜市（傍晚）', '5-7 零号核心', '缝纫机首领')]
COLS = [('现在', None), ('A · 糖果街机', 'A'), ('B · 晴天涂鸦', 'B'), ('C · 柠檬汽水', 'C')]
def sheet(now_dir, mock_dir, dst):
    tw, th, pad, lw, gap = 600, 338, 24, 230, 12
    W = pad + lw + len(COLS) * (tw + gap) - gap + pad; top = 160
    H = top + len(ROWS) * (th + gap) + 70 + pad
    sh = Image.new('RGB', (W, H), (250, 250, 247)); dr = ImageDraw.Draw(sh)
    dr.text((pad, 22), '五章首领战 · 三个亮色方向逐章对比', font=fT, fill=INK)
    dr.text((pad, 84), '只是样图，游戏没改。首领都换成回收机器人首领；背景都是首领房间的分层原图让 Gemini 重画的，主角、血条、伤害数字是实机抠出来的。', font=fS, fill=SUB)
    for c, (t, _) in enumerate(COLS):
        dr.text((pad + lw + c * (tw + gap), top - 40), t, font=fH, fill=INK)
    for r, (ch, name, stage, boss) in enumerate(ROWS):
        y = top + r * (th + gap)
        dr.text((pad, y + 8), ch, font=fH, fill=INK); dr.text((pad, y + 48), name, font=fM, fill=SUB)
        dr.text((pad, y + 84), stage, font=fS, fill=MUTE); dr.text((pad, y + 118), boss, font=fS, fill=(214, 92, 40))
        for c, (_, d) in enumerate(COLS):
            p = os.path.join(now_dir, 'b%da_full.png' % (r + 1)) if d is None else os.path.join(mock_dir, 'boss_%d_%s.jpg' % (r + 1, d))
            sh.paste(Image.open(p).convert('RGB').resize((tw, th), Image.LANCZOS), (pad + lw + c * (tw + gap), y))
    y = top + len(ROWS) * (th + gap) + 14
    dr.text((pad, y), '首领名字和血量那一行原来是白字，亮底上垫了白底、换成深色字。血条框还是现在的深色金属框，整套换亮色时要一起换。', font=fS, fill=(60, 64, 80))
    sh.save(dst, quality=86); print(dst, sh.size)
if __name__ == '__main__':
    sheet(*sys.argv[1:4])
