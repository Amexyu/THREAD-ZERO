# v0.20 把背景图（远景 / 中景）的边长补成 4 的倍数：Unity 的 BC7 压缩要求宽高都是 4 的倍数，不然整张退回不压缩
#（规则见 ThreadZero/Assets/Editor/ArtImport.cs，检查用 Unity 菜单「Thread Zero/检查/压缩画质（逐张和原 PNG 比）」）。
#   高：在图最下面重复最后一行。游戏里远景、中景铺满整个舞台高度，最下面几行压在地面带后面，看不见。
#   宽：远景、中景要横向无缝平铺，不能补边，按比例缩到最近的 4 的倍数（最多差 2 像素）。
# 用法：python3 pad4.py 图.png [更多.png] [--backup 目录]
#   原地改写；给了 --backup 就先把原图复制到「目录/给的相对路径」（已有备份不覆盖）。bg_scene.py 出图时会自动调用。
import sys, os, shutil
from PIL import Image

def align4(im):
    w, h = im.size
    if w % 4: im = im.resize((max(4, round(w / 4) * 4), h), Image.LANCZOS)
    p = (-h) % 4
    if p:
        out = Image.new(im.mode, (im.width, h + p)); out.paste(im, (0, 0))
        last = im.crop((0, h - 1, im.width, h))
        for i in range(p): out.paste(last, (0, h + i))
        im = out
    return im

if __name__ == '__main__':
    args = sys.argv[1:]; backup = None
    if '--backup' in args: k = args.index('--backup'); backup = args[k + 1]; del args[k:k + 2]
    for f in args:
        with Image.open(f) as src: src.load(); im = src.copy()
        w, h = im.size
        if w % 4 == 0 and h % 4 == 0: print('已对齐 ', f, w, h); continue
        if backup:
            dst = os.path.join(backup, f)
            if not os.path.exists(dst): os.makedirs(os.path.dirname(dst), exist_ok=True); shutil.copy2(f, dst)
        out = align4(im); out.save(f)
        print('补边   ', f, '%dx%d → %dx%d' % (w, h, out.width, out.height))
