"""v0.29 界面皮肤贴图：和例图同一套画法（skin.py），按 2 像素 / 逻辑单位导出到 Resources/UI/Skin。
游戏里用 9 宫格拉伸画（UiSkin.cs 里每张图的边宽和这里一致）。字体路径是出图机器上的（见 mk.py），在仓库里只当记录。
用法：python3 make_skin.py <输出目录>"""
import sys, os
import mk
mk.SS = 2.0                                  # 贴图：每逻辑单位 2 像素
from skin import *

OUT = sys.argv[1] if len(sys.argv) > 1 else 'out'
os.makedirs(OUT, exist_ok=True)
def save(im, name): im.save(os.path.join(OUT, name + '.png')); print(name, im.size)

# ---------- 面板 ----------
def panel_plastic():
    im = canvas(120, 120)
    lshadow(im, 24, 24, 72, 72, 18, blur=11, alpha=.38, dy=6)
    fill_round(im, 24, 24, 72, 72, 18, PL_EDGE, mix(C(PL_EDGE), (0, 0, 0, 255), .25))
    fill_round(im, 25.2, 25.2, 69.6, 67.1, 17, PL_HI, PL_LO)
    fill_round(im, 25.2 + 8, 25.2, 69.6 - 16, 2.6, 1.3, (255, 255, 255, 230))
    save(im, 'panel_plastic')                       # 边 52，外扩 24

def panel_recess():
    im = canvas(84, 84)
    recess(im, 0, 0, 84, 84, r=14, top=hx('CFCAC0'), bottom=hx('E2DED6'), depth=14)
    save(im, 'panel_recess')                        # 边 34

def panel_metal():
    im = canvas(104, 104)
    lshadow(im, 20, 20, 64, 64, 14, blur=9, alpha=.45, dy=5)
    fill_round(im, 20, 20, 64, 64, 14, hx('0C0D0F'))
    fill_round(im, 21.5, 21.5, 61, 61, 13, hx('303237'), hx('17181B'))
    fill_round(im, 23, 21.5, 58, 1.4, .7, (255, 255, 255, 60))
    save(im, 'panel_metal')                         # 边 46，外扩 20

def panel_paper():
    im = canvas(64, 64)
    lshadow(im, 12, 12, 40, 40, 4, blur=5, alpha=.28, dy=3)
    fill_round(im, 12, 12, 40, 40, 4, hx('FFFFFF'), hx('F6F4EE'))
    save(im, 'panel_paper')                         # 边 22，外扩 12

def panel_glass():
    im = canvas(64, 64)
    fill_round(im, 0, 0, 64, 64, 16, (255, 255, 255, 18)); stroke_round(im, 0, 0, 64, 64, 16, (255, 255, 255, 34), 1)
    save(im, 'panel_glass')                         # 边 24

# ---------- 按键 ----------
FACES = {'white': (PL_HI, hx('E2DFD8'), hx('B7B2A8')), 'orange': (hx('FF6A33'), hx('F0420C'), hx('A82A06')),
         'dark': (hx('3A3C41'), hx('222327'), hx('0E0F11')), 'ghost': (hx('E6E3DC'), hx('D8D4CB'), hx('B5B0A6'))}
def key(kind, down=False):
    top, bot, side = FACES[kind]
    im = canvas(72, 64)
    oy = 3 if down else 0; th = 0 if down else 4.5
    lshadow(im, 8, 8 + oy, 56, 48 - oy, 12, blur=4, alpha=.32, dy=3)
    fill_round(im, 8, 8 + oy, 56, 48 - oy, 12, side)
    fill_round(im, 9, 9 + oy, 54, 46 - oy - th, 11, top, bot)
    fill_round(im, 8 + 7, 9.6 + oy, 56 - 14, 2, 1, (255, 255, 255, 120 if kind != 'dark' else 40))
    save(im, 'key_' + kind + ('_down' if down else ''))   # 边 26，外扩 8

def key_mask():
    im = canvas(72, 64); fill_round(im, 9, 9, 54, 41.5, 11, (255, 255, 255, 255)); save(im, 'key_mask')

def lcd_panel():
    im = canvas(40, 40)
    fill_round(im, 0, 0, 40, 40, 6, hx('101113')); stroke_round(im, 0, 0, 40, 40, 6, (255, 255, 255, 40), 1)
    fill_round(im, 3, 3, 34, 15, 4, (255, 255, 255, 14))
    save(im, 'lcd')                                 # 边 12

# ---------- 卡片 ----------
def card_layers():
    im = canvas(64, 64); s2 = canvas(64, 64)
    ImageDraw.Draw(s2).rounded_rectangle(box(14, 14, 36, 36), radius=P(9), fill=(0, 0, 0, 160))
    save(s2.filter(ImageFilter.GaussianBlur(P(5.5))), 'card_shadow')   # 边 28，外扩 14
    im = canvas(40, 40); fill_round(im, 0, 0, 40, 40, 9, hx('0A0B0C')); save(im, 'card_outer')            # 边 14
    im = canvas(40, 40); fill_round(im, 0, 0, 40, 40, 8, hx('FFFFFF'), hx('8C8C8C')); save(im, 'card_rim')   # 边 14（按品质着色）
    im = canvas(40, 40); fill_round(im, 0, 0, 40, 40, 6.5, CARD_T, CARD_B); save(im, 'card_body')        # 边 14
    im = canvas(40, 40); fill_round(im, 0, 0, 40, 40, 3, hx('FFFFFF'), hx('7A7A7A')); save(im, 'card_stripe')   # 边 6
    im = canvas(40, 40)
    fill_round(im, 0, 0, 40, 40, 5, hx('15161A'), hx('0B0C0E'))
    ins = canvas(40, 40); ImageDraw.Draw(ins).rounded_rectangle(box(-10, -10, 60, 18), radius=P(5), fill=(0, 0, 0, 120))
    ins = ins.filter(ImageFilter.GaussianBlur(P(4))); ins.putalpha(ImageChops.multiply(ins.split()[3], mask_round(ins.size, P(5)))); im.alpha_composite(ins)
    stroke_round(im, 0, 0, 40, 40, 5, (255, 255, 255, 30), 1)
    save(im, 'card_art')                            # 边 14
    im = canvas(48, 48)                             # 选中：橙色外框
    stroke_round(im, 4, 4, 40, 40, 11, OR, 2.8); save(im, 'card_select')   # 边 18，外扩 4
    im = canvas(40, 40); stroke_round(im, .5, .5, 39, 39, 8, (255, 255, 255, 255), 1); save(im, 'card_line')   # 边 12

def art_fx():
    w, h = 100, 80
    rays = canvas(w, h); dr = ImageDraw.Draw(rays)
    for k in range(5):
        a0 = -1.9 + k * .32; cx0, cy0 = w * .5, h * 1.05
        dr.polygon([(P(cx0), P(cy0)), (P(cx0 + math.cos(a0) * h * 2), P(cy0 + math.sin(a0) * h * 2)),
                    (P(cx0 + math.cos(a0 + .1) * h * 2), P(cy0 + math.sin(a0 + .1) * h * 2))], fill=(255, 255, 255, 60))
    save(rays, 'fx_rays')
    sh = canvas(100, 100); ds = ImageDraw.Draw(sh)
    for k in range(3):
        sx0 = 100 * (.12 + k * .3)
        ds.polygon([(P(sx0), 0), (P(sx0 + 9), 0), (P(sx0 - 14), P(100)), (P(sx0 - 23), P(100))], fill=(255, 255, 255, 60))
    save(sh, 'fx_sheen')
    sp = canvas(16, 16); d2 = ImageDraw.Draw(sp); c, rr = 8, 3.2
    d2.polygon([(P(c), P(c - rr * 2)), (P(c + rr * .5), P(c)), (P(c), P(c + rr * 2)), (P(c - rr * .5), P(c))], fill=(255, 255, 255, 255))
    d2.polygon([(P(c - rr * 2), P(c)), (P(c), P(c - rr * .5)), (P(c + rr * 2), P(c)), (P(c), P(c + rr * .5))], fill=(255, 255, 255, 255))
    save(sp, 'fx_sparkle')
    ht = canvas(128, 128); halftone(ht, 0, 0, 128, 128, (255, 255, 255, 255), step=6.5, rmax=1.6, origin='br', reach=1.0); save(ht, 'fx_halftone')
    big = canvas(400, 300); halftone(big, 0, 0, 400, 300, (255, 255, 255, 255), step=11, rmax=2.5, origin='bl', reach=1.0); save(big, 'fx_halftone_big')

def shapes():
    im = canvas(32, 32); fill_round(im, 0, 0, 32, 32, 16, hx('FFFFFF')); save(im, 'disk')
    im = canvas(32, 32); ImageDraw.Draw(im).ellipse(box(1, 1, 30, 30), outline=(255, 255, 255, 255), width=P(1.6)); save(im, 'ring')
    im = canvas(256, 256); fill_round(im, 0, 0, 256, 256, 128, hx('FFFFFF')); save(im, 'disk_big')        # 大圆（镜子这种大胶囊形，Skin.Stadium）
    im = canvas(40, 20); fill_round(im, 0, 0, 40, 20, 10, hx('FFFFFF')); save(im, 'pill')                    # 左右边 10
    im = canvas(40, 20); stroke_round(im, .5, .5, 39, 19, 9.5, (255, 255, 255, 255), 1); save(im, 'pill_line')
    im = canvas(24, 24); fill_round(im, 0, 0, 24, 24, 4, hx('FFFFFF')); save(im, 'round4')                    # 边 6
    im = canvas(24, 24); stroke_round(im, .5, .5, 23, 23, 4, (255, 255, 255, 255), 1); save(im, 'round4_line')
    for name, r in (('gem', 5.0), ('gem_out', 6.3)):
        im = canvas(14, 14); d = ImageDraw.Draw(im); c = 7
        d.polygon([(P(c), P(c - r)), (P(c + r), P(c)), (P(c), P(c + r)), (P(c - r), P(c))], fill=(255, 255, 255, 255)); save(im, name)
    im = canvas(14, 14); d = ImageDraw.Draw(im); c, r = 7, 5.0
    d.polygon([(P(c), P(c - r)), (P(c + r), P(c)), (P(c), P(c))], fill=(255, 255, 255, 255)); save(im, 'gem_hi')
    # 角标（左上斜缎带的底色，字在游戏里画）
    im = canvas(44, 44); d = ImageDraw.Draw(im)
    d.polygon([(0, P(22)), (P(22), 0), (P(40), 0), (0, P(40))], fill=C(OR))
    with over(im) as dl: dl.line([0, P(23.5), P(23.5), 0], fill=(255, 255, 255, 180), width=P(1))
    save(im, 'ribbon_corner')
    # 圆贴纸（白边 + 投影，里面的颜色在游戏里用 disk 着色）
    im = canvas(40, 40); d = ImageDraw.Draw(im)
    fill_round(im, 4, 5.5, 32, 32, 16, (0, 0, 0, 70)); fill_round(im, 4, 4, 32, 32, 16, hx('FFFFFF')); save(im, 'sticker')   # 内圆：中心 20,20 半径 14
    im = canvas(40, 30); fill_round(im, 3, 4.5, 34, 22, 5, (0, 0, 0, 80)); fill_round(im, 2, 2, 36, 24, 5, hx('FFFFFF')); save(im, 'tag_back')   # 边 8，外扩 2
    im = canvas(56, 40)    # 印章外框（白色，游戏里着色），斑驳
    d = ImageDraw.Draw(im)
    d.rounded_rectangle(box(4, 4, 48, 32), radius=P(5), outline=(255, 255, 255, 230), width=P(2.4))
    d.rounded_rectangle(box(7, 7, 42, 26), radius=P(3), outline=(255, 255, 255, 170), width=P(1))
    random.seed(11); a = im.split()[3]; px = a.load()
    for _ in range(int(im.size[0] * im.size[1] * .06)):
        i, j = random.randrange(im.size[0]), random.randrange(im.size[1]); px[i, j] = int(px[i, j] * .25)
    im.putalpha(a.filter(ImageFilter.GaussianBlur(.6))); save(im, 'stamp_frame')   # 边 12
    t = canvas(56, 16); tape(t, 0, 0, 56, 16, angle=0); save(t, 'tape')
    sc = canvas(12, 12); screw(sc, 6, 6, 4.2); save(sc, 'screw')
    bc = canvas(80, 14); barcode(bc, 1, 1, 78, 12, hx('FFFFFF')); save(bc, 'barcode')

# ---------- 柜子零件 ----------
def cabinet_bits():
    t = 8
    im = canvas(40, 22)
    lshadow(im, 0, 3, 40, t, t / 2, blur=3, alpha=.35, dy=4)
    g = grad_img((P(40), P(t)), C(hx('FFFFFF')), C(hx('8F949B'))); g2 = grad_img((P(40), P(t * .45)), C(hx('E9ECEF')), C(hx('6F747B')))
    bar = Image.new('RGBA', (P(40), P(t)), (0, 0, 0, 0)); bar.alpha_composite(g); bar.alpha_composite(g2, (0, P(t * .55)))
    bar.putalpha(ImageChops.multiply(bar.split()[3], mask_round(bar.size, P(t / 2))))
    im.alpha_composite(bar, (0, P(3))); save(im, 'rail')                     # 左右边 8；杆在 y 3~11
    im = canvas(16, 24)
    fill_round(im, 1, 1, 14, 20, 3, hx('F2F0EB'), hx('BDB8AE')); stroke_round(im, 1, 1, 14, 20, 3, (0, 0, 0, 70), 1); screw(im, 8, 11, 3)
    save(im, 'rail_end')
    im = canvas(30, 14); fill_round(im, 2, 2, 26, 10, 3, hx('F7F6F2'), hx('CFCBC2')); stroke_round(im, 2, 2, 26, 10, 3, (0, 0, 0, 60), 1); save(im, 'hook')
    im = canvas(34, 44)    # 黑夹子（顶上金属环，竖印 TZ）
    w, h = 22, 30; ox = 6
    lshadow(im, ox, 12, w, h, 4, blur=3, alpha=.35, dy=2)
    ImageDraw.Draw(im).ellipse(box(ox + w / 2 - 5, 2, 10, 10), outline=C(hx('C9CCD0')), width=P(2))
    fill_round(im, ox, 8, w, h, 4, hx('3A3C40'), C(RUB)); fill_round(im, ox + 1, 9, w - 2, 2.2, 1, (255, 255, 255, 70))
    fill_round(im, ox + w / 2 - 4, 8 + h - 2, 8, 6, 2, hx('17181A'))
    lab = canvas(h - 10, w - 6); text(lab, (h - 10) / 2, (w - 6) / 2, 'TZ', 8.5, C(hx('E8E8E4')), 'n', 'mm')
    lab = lab.rotate(90, expand=True); im.alpha_composite(lab, (P(ox + 3), P(8 + 4)))
    save(im, 'clip')                                 # 夹子中线 x = 17；卡片上沿对齐 y = 32
    im = canvas(10, 14); fill_round(im, 2, 0, 6, 12, 3, hx('C8CBD0'), hx('7C8188')); stroke_round(im, 2, 0, 6, 12, 3, (0, 0, 0, 90), .8); save(im, 'peg')
    im = canvas(60, 44)    # 灯带：中线 + 光晕（左右边 22）
    lay = canvas(60, 44); fill_round(lay, 14, 20, 32, 4, 2, (255, 255, 255, 255)); lay = lay.filter(ImageFilter.GaussianBlur(P(6)))
    im.alpha_composite(lay); im.alpha_composite(lay)
    fill_round(im, 14, 20, 32, 4, 2, (255, 255, 255, 255))
    save(im, 'led')
    tile = canvas(15, 15); d = ImageDraw.Draw(tile); r = 1.7; cx = cy = 7.5
    with over(tile) as d2: d2.ellipse(box(cx - r, cy - r + .6, 2 * r, 2 * r), fill=(255, 255, 255, 120))
    with over(tile) as d2: d2.ellipse(box(cx - r, cy - r, 2 * r, 2 * r), fill=(70, 66, 60, 150))
    save(tile, 'tile_pegboard')
    for name, c1, c2 in (('tile_hazard', OR, INK2), ('tile_hazard_grey', hx('C9C5BC'), hx('B5B0A6'))):
        tile = canvas(18, 18); d = ImageDraw.Draw(tile); d.rectangle([0, 0, tile.size[0], tile.size[1]], fill=C(c1))
        for k in (-18, 0, 18):
            d.polygon([(P(k), P(18)), (P(k + 9), P(18)), (P(k + 9 + 18), 0), (P(k + 18), 0)], fill=C(c2))
        save(tile, name)
    tile = canvas(40, 40); d = ImageDraw.Draw(tile)
    d.line([0, 0, P(40), 0], fill=(255, 255, 255, 255), width=P(1)); d.line([0, 0, 0, P(40)], fill=(255, 255, 255, 255), width=P(1))
    save(tile, 'tile_grid')
    im = canvas(200, 44); d = ImageDraw.Draw(im)
    d.ellipse(box(4, 6, 192, 34), fill=C(hx('1C1D20'))); d.ellipse(box(4, 2, 192, 34), fill=C(hx('2E3034')))
    d.ellipse(box(12, 6, 176, 26), outline=C(OR), width=P(2.4))
    save(im, 'platform')

for f in (panel_plastic, panel_recess, panel_metal, panel_paper, panel_glass, key_mask, lcd_panel, card_layers, art_fx, shapes, cabinet_bits):
    f()
for k in FACES: key(k); key(k, True)
