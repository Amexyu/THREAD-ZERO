# 卡35 预览：每行一只怪，左三格原版（idle / windup / strike），右三格新版；浅底 + 脚底线，看大小、朝向、抠图边。
# 用法：python3 preview35.py <输出.jpg> <怪名> [怪名…]
import sys, os
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__)); E = os.path.join(HERE, '..', '..', 'ThreadZero', 'Assets', 'Resources', 'Art', 'V16', 'Enemies')
out, names = sys.argv[1], sys.argv[2:]
S = 256; W = 6 * S + 40; H = len(names) * S
sheet = Image.new('RGB', (W, H), (243, 243, 239)); d = ImageDraw.Draw(sheet)
for i, n in enumerate(names):
    for j, pose in enumerate(['idle', 'windup', 'strike']):
        for k, (folder, x0) in enumerate([(E, 0), (os.path.join(HERE, '切图'), 3 * S + 40)]):
            p = os.path.join(folder, n + '_' + pose + '.png')
            x = x0 + j * S; y = i * S
            d.rectangle([x + 2, y + 2, x + S - 3, y + S - 3], fill=(232, 236, 240) if k else (225, 225, 220))
            if os.path.exists(p):
                im = Image.open(p).convert('RGBA').resize((S - 8, S - 8), Image.LANCZOS); sheet.paste(im, (x + 4, y + 4), im)
            d.line([x + 4, y + 4 + int(486 * (S - 8) / 512), x + S - 4, y + 4 + int(486 * (S - 8) / 512)], fill=(200, 120, 120))
    d.text((3 * S + 6, i * S + 6), n, fill=(60, 60, 60))
sheet.save(out, quality=88); print('ok', out)
