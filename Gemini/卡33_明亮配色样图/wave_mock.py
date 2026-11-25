# 第二～五章敌人样图：实机里怪刚走过来的那一刻（还没贴到主角身上），把原版怪原地换成 Gemini 重画的回收机器人。
#   每章选一张早一点的截图（w66），去掉原版怪（连通块），血条 / 锁定框 / 伤害数字 / 主角不动，
#   新怪按原版在游戏里的大小（可见范围的面积）缩放，中心对准它头上的血条，脚底对准原来的落脚线。
#   用法: python3 wave_mock.py <章> <亮色背景> <cute 目录> <输出.jpg>
import sys, os, json, numpy as np, cv2
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)   # ui_action.py 在同一个文件夹
import ui_action as UA
INFO = json.load(open(os.path.join(HERE, 'orig_info.json')))
NAMES = json.load(open(os.path.join(HERE, 'names.json')))
WAVE = os.path.join(HERE, '出图', '实机前景_w66')   # 桥 w66 截图抠出来的前景
# remove: 要去掉的连通块（alpha>60、8 连通的编号）；bars: 血条和怪粘在一起被一起去掉的，按 (中心 x, 上沿 y) 补一根；
# protect: 锁定框（橙色细线）所在范围，去怪的时候这里的橙色细线留着；place: (种类 0~5, 中心 x, 脚底 y)，按先后画（后画的在前面）
CFG = {
    '2': dict(shot='c2b', remove=[38, 43, 44], bar_tpl=42, bars=[(1500, 640)], protect=[(1015, 600, 1205, 860)], banner=True,
              place=[(2, 1440, 806), (0, 1500, 805), (0, 1115, 793), (0, 1760, 810)],
              corners=[(1035, 640, 1195, 844, 'bl'), (1035, 640, 1195, 844, 'br')]),   # 锁定框下面两个角原来被提示条挡住，补全
    '3': dict(shot='c3b', remove=[49, 48, 43, 47, 44], bar_tpl=41, bars=[], protect=[], banner=False,
              place=[(2, 1530, 815), (0, 1635, 814), (0, 1130, 805), (0, 1732, 798), (2, 1957, 815), (0, 1013, 826)]),
    '4': dict(shot='c4c', remove=[39, 47, 46, 45], bar_tpl=43, bars=[(962, 642)], protect=[(868, 600, 1062, 860)], banner=False,
              place=[(2, 991, 807), (2, 1296, 811), (1, 1601, 832), (1, 1723, 838), (0, 1148, 810), (0, 1403, 805), (0, 962, 818)]),
    '5': dict(shot='c5b', remove=[45, 47, 46, 40], bar_tpl=42, bars=[(1761, 643)], protect=[(900, 640, 1080, 860)], banner=False,
              place=[(2, 1821, 809), (0, 1761, 807), (0, 1292, 820), (0, 1113, 823), (0, 989, 813)]),
}
def trim(im):
    a = np.asarray(im)[..., 3]; ys, xs = np.where(a > 30); return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
def fit(cute, name, extra=1.0):
    o = INFO[name]; ow, oh = o['w'], o['h']; c = trim(cute); cw, ch = c.size
    k = ((ow * oh) / (cw * ch)) ** 0.5 * extra
    k = min(k, 1.2 * oh / ch, 1.3 * ow / cw)
    return c.resize((max(1, round(cw * k)), max(1, round(ch * k))), Image.LANCZOS)
def build(ch, bg, cute_dir, dst):
    c = CFG[ch]; fg = Image.open(os.path.join(WAVE, 'fg_%s.png' % c['shot'])).convert('RGBA'); a = np.asarray(fg).copy()
    m = (a[..., 3] > 60).astype(np.uint8); n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    # 补血条用的模板（一根完整的怪血条）
    x, y, w, h = st[c['bar_tpl']][:4]; tpl = a[y:y + h, x:x + w].copy(); tpl[..., 3] = np.where(lab[y:y + h, x:x + w] == c['bar_tpl'], tpl[..., 3], 0)
    rm = np.isin(lab, c['remove']).astype(np.uint8)
    rm = cv2.dilate(rm, np.ones((5, 5), np.uint8)) > 0
    keep = np.zeros(rm.shape, bool)
    r, g, b, al = [a[..., i].astype(int) for i in range(4)]
    orange = (r > 215) & (g > 50) & (g < 140) & (b < 80) & (al > 150)
    for x0, y0, x1, y1 in c['protect']:
        box = np.zeros(rm.shape, bool); box[y0:y1, x0:x1] = True
        thin = orange & ~(cv2.morphologyEx(orange.astype(np.uint8), cv2.MORPH_OPEN, np.ones((9, 9), np.uint8)) > 0)
        keep |= box & cv2.dilate(thin.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool) & (al > 20)
    a[rm & ~keep, 3] = 0
    if c['banner']:   # 第二章那张截图下面有一条一次性提示（机库开放了……），样图里拿掉
        dark = (a[..., 3] > 200) & (a[..., :3].max(-1) < 40); ys, xs = np.where(dark[800:960, 400:1500])
        if len(xs):
            x0, x1, y0, y1 = 400 + xs.min(), 400 + xs.max(), 800 + ys.min(), 800 + ys.max()
            hero = np.zeros(rm.shape, bool); hero[:, 660:890] = True
            clr = np.zeros(rm.shape, bool); clr[y0 - 2:y1 + 3, x0 - 18:x1 + 3] = True   # 左边那条橙色竖条也一起拿掉
            clr &= ~(hero & (np.arange(rm.shape[0])[:, None] < y0 + 8))
            a[clr & ~keep, 3] = 0
    for x0, y0, x1, y1, which in c.get('corners', []):   # 锁定框的角：橙色 L 形，粗 5、边长 31（照实机量的）
        T, L = 5, 31; col = (255, 74, 18, 255)
        xs = x0 if 'l' in which else x1 - L + 1; xv = x0 if 'l' in which else x1 - T + 1
        ys = y1 - T + 1 if 'b' in which else y0; yv = y1 - L + 1 if 'b' in which else y0
        a[ys:ys + T, xs:xs + L] = col; a[yv:yv + L, xv:xv + T] = col
    out = Image.fromarray(a, 'RGBA')
    t = Image.fromarray(tpl, 'RGBA')
    for cx, by in c['bars']: out.alpha_composite(t, (int(cx - t.width / 2), int(by)))
    names = NAMES[ch]
    cache = {}
    for k, cx, foot in c['place']:
        if k not in cache: cache[k] = fit(Image.open(os.path.join(cute_dir, 'cute_%d.png' % k)).convert('RGBA'), names[k])
        s = cache[k]; out.alpha_composite(s, (int(round(cx - s.width / 2)), int(round(foot - s.height))))
    tmp = dst + '.fg.png'; out.save(tmp)
    UA.compose(bg, tmp, dst, shadow=True)   # 和第一章一样：白色伤害数字垫一圈淡影
    return tmp
if __name__ == '__main__':
    build(*sys.argv[1:5])
