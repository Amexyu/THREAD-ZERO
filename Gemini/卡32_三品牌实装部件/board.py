# 把几个拼装预览排成一排，下面写名字。用法：python3 board.py out.jpg "名字=slot:id,slot:id" …   （空的 = 初始外观）
import sys, os, importlib
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
FONT = '/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'
def one(spec, unit, bg, pose):
    import rig_preview as R; R = importlib.reload(R)
    for kv in filter(None, spec.split(',')):
        s, pid = kv.split(':'); R.apply(s, pid)
    return R.render('/tmp/_one.png', unit, bg, pose)
def board(out, items, unit=2.6, bg=(50, 52, 60), pose=None, cols=None):
    tiles = []
    f = ImageFont.truetype(FONT, 26)
    for name, spec in items:
        im = one(spec, unit, bg, pose).convert('RGB'); d = ImageDraw.Draw(im)
        w = d.textlength(name, font=f); d.text(((im.width - w) / 2, im.height - 40), name, font=f, fill=(235, 235, 240))
        tiles.append(im)
    cols = cols or len(tiles); rows = (len(tiles) + cols - 1) // cols
    W, H = tiles[0].size; sh = Image.new('RGB', (cols * W + (cols - 1) * 6, rows * H + (rows - 1) * 6), (15, 15, 18))
    for i, t in enumerate(tiles): sh.paste(t, ((i % cols) * (W + 6), (i // cols) * (H + 6)))
    sh.save(out, quality=90); print(out, sh.size)
if __name__ == '__main__':
    a = sys.argv[1:]; out = a[0]; items = []; cols = None; unit = 2.6
    for x in a[1:]:
        if x.startswith('--cols='): cols = int(x[7:]); continue
        if x.startswith('--unit='): unit = float(x[7:]); continue
        n, s = x.split('=', 1); items.append((n, s))
    board(out, items, unit=unit, cols=cols)
