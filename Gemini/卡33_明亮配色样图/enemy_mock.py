# 敌人样图：把 Gemini 重画的「离家出走的回收机器人」放进 1-1 亮色样图里。
#   wave：实机第 3 波的 3 只机械狗原地换成扫地机器人（同样高度、脚底对齐），其它（主角、血条、锁定框）不动。
#   lineup：主角和 6 种新敌人按游戏里的画布大小站在步道上（看同框比例）。
import sys, os, numpy as np, cv2
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # ui_action.py 在同一个文件夹
import ui_action as UA
# 原版精灵在 512 画布里的可见高度比例、游戏里的画布（逻辑像素，×1.2 = 1920 宽截图里的像素）
ORIG_FRAC = [391 / 512, 344 / 512, 419 / 512, 362 / 512, 403 / 512, 353 / 512]
CANVAS = [160, 200, 220, 220 * 1.2, 200 * 1.2, 380]
NAMES = ['扫地机器狗', '电饭锅无人机', '冰箱重型', '洗衣机精英', '旧电视信标', '售货机首领']
def trim(im):
    a = np.asarray(im)[..., 3]; ys, xs = np.where(a > 30); return im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
def scaled(im, h):
    k = h / im.height; return im.resize((max(1, round(im.width * k)), max(1, round(h))), Image.LANCZOS)
def wave(fg_path, cute_dir, dst_fg):
    fg = Image.open(fg_path).convert('RGBA'); a = np.asarray(fg).copy()
    m = (a[..., 3] > 60).astype(np.uint8); n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    dogs = [i for i in range(1, n) if st[i][4] > 8000 and st[i][0] > 1300 and st[i][1] > 600]
    dog = trim(Image.open(os.path.join(cute_dir, 'cute_0.png')).convert('RGBA'))
    boxes = []
    for i in dogs:
        x, y, w, h = st[i][:4]; boxes.append((x, y, w, h))
        rm = cv2.dilate((lab == i).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
        a[rm, 3] = 0
    # 主角脚边那堆黑色残骸是旧机械狗被打爆后的残骸（旧画风），一并拿掉
    for i in range(1, n):
        x, y, w, h = st[i][:4]
        if st[i][4] > 5000 and w > 2.5 * h and y > 740 and x < 900:
            a[cv2.dilate((lab == i).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0, 3] = 0
    out = Image.fromarray(a, 'RGBA')
    for x, y, w, h in sorted(boxes):
        s = scaled(dog, h * 0.98); cx = x + w / 2
        if x + w >= 1919: cx = x + 159 / 2            # 第三只被屏幕右边切掉：按完整宽度估中心
        out.alpha_composite(s, (int(round(cx - s.width / 2)), int(round(y + h - s.height))))
    out.save(dst_fg); print(dst_fg, 'replaced', len(boxes))
def lineup(bg_path, fg_path, cute_dir, dst, ground=822):
    from PIL import ImageFilter
    bg = UA.Image.open(bg_path).convert('RGB').resize((1920, 1080), Image.LANCZOS).convert('RGBA')
    fg = Image.open(fg_path).convert('RGBA'); a = np.asarray(fg)
    m = (a[..., 3] > 60).astype(np.uint8); n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    hero_i = max(range(1, n), key=lambda i: st[i][4] if 560 < st[i][0] < 760 and st[i][1] > 480 else 0)
    x, y, w, h = st[hero_i][:4]
    hero = Image.fromarray(np.where((lab == hero_i)[..., None], a, 0).astype(np.uint8), 'RGBA').crop((x, y, x + w, y + h))
    items = [(hero, 0)]
    for k in range(6):
        im = trim(Image.open(os.path.join(cute_dir, 'cute_%d.png' % k)).convert('RGBA'))
        items.append((scaled(im, CANVAS[k] * 1.2 * ORIG_FRAC[k]), 150 if k == 1 else 0))   # 无人机悬空
    total = sum(i.width for i, _ in items); gap = (1920 - 80 - total) / (len(items) - 1)
    xx = 40
    for im, lift in items:
        sh = Image.new('RGBA', bg.size, (0, 0, 0, 0)); d = UA.ImageDraw.Draw(sh)
        d.ellipse((xx + im.width * .15, ground - 9, xx + im.width * .85, ground + 9), fill=(40, 40, 70, 60))
        bg.alpha_composite(sh.filter(ImageFilter.GaussianBlur(4)))
        bg.alpha_composite(im, (int(xx), int(ground - lift - im.height))); xx += im.width + gap
    bg.convert('RGB').save(dst, quality=92); print(dst)
if __name__ == '__main__':
    cmd = sys.argv[1]
    if cmd == 'wave': wave(*sys.argv[2:5])
    else: lineup(*sys.argv[2:6])
