"""模拟 StageArt.Background 的三层合成：远景/中景按舞台高 900 等比缩放横向平铺，地面带高 (900-690)/.8，顶在 690-.2h。
用法: stagemock.py far.png mid.png ground.png out.jpg [--scroll X]"""
import sys, numpy as np
from PIL import Image
SW, SH, GY = 1600, 900, 690
def layer(canvas, img, h, y, scroll):
    w = max(1, round(h * img.width / img.height))
    t = img.resize((w, round(h)), Image.LANCZOS).convert('RGBA')
    x = -int(scroll) % w - w
    while x < SW:
        canvas.alpha_composite(t, (x, int(round(y)))) if y >= 0 else canvas.alpha_composite(t.crop((0, -int(y), w, t.height)), (x, 0))
        x += w
def mock(far, mid, ground, out, scroll=0.0, scale=1.0):
    c = Image.new('RGBA', (SW, SH), (0, 0, 0, 255))
    layer(c, Image.open(far), SH, 0, scroll * .25)
    if mid: layer(c, Image.open(mid), SH, 0, scroll * .6)
    if ground:
        h = (SH - GY) / .8
        layer(c, Image.open(ground), h, GY - h * .2, scroll)
    c = c.convert('RGB')
    if scale != 1: c = c.resize((int(SW * scale), int(SH * scale)), Image.LANCZOS)
    c.save(out, quality=88)
if __name__ == '__main__':
    a = sys.argv[1:]
    sc = float(a[a.index('--scroll') + 1]) if '--scroll' in a else 0.0
    mock(a[0], a[1] if a[1] != '-' else None, a[2] if a[2] != '-' else None, a[3], sc)
