# 抠洋红背景时，衣服、鞋上「洋红/粉色颜料点」也被当成背景抠成了透明或半透明。
# 只有和图片外圈连通的才是真背景；被包在里面的这些像素，回原图取它本来的颜色，改成不透明。
import os, json, numpy as np, cv2
from PIL import Image
SRC = '../卡42_主角标准站立图'; FPU = 10.25; K = 5.0 / FPU
RA = np.asarray(Image.open(os.path.join(SRC, '出图/_A.png')).convert('RGB')).astype(np.float32)
RB = np.asarray(Image.open(os.path.join(SRC, '出图/_B_aligned.png')).convert('RGB')).astype(np.float32)
arm = np.load(os.path.join(SRC, '出图/_armmask.npy')).astype(np.uint8); hand = np.load(os.path.join(SRC, '出图/_handmask.npy')).astype(bool)
fillm = cv2.dilate(arm, np.ones((9, 9), np.uint8)).astype(bool)
RT = RA.copy(); RT[fillm] = RB[fillm]                                      # 身体用的原图：手臂那块换成 B
def holes_of(a):
    out = (a < 128).astype(np.uint8); h, w = out.shape
    ff = np.pad(out, 1, constant_values=1); mask = np.zeros((h + 4, w + 4), np.uint8); cv2.floodFill(ff, mask, (0, 0), 2)
    ext = ff[1:-1, 1:-1] == 2; hole = (~ext) & (a < 250)
    return hole & (cv2.distanceTransform((~ext).astype(np.uint8), cv2.DIST_L2, 3) > 1.5)
def restore(path_in, path_out, raw, x0, y0):
    im = np.asarray(Image.open(path_in).convert('RGBA')).astype(np.float32); hole = holes_of(im[..., 3]); ys, xs = np.nonzero(hole)
    for py, px in zip(ys, xs):
        ax0, ax1 = x0 + px / K, x0 + (px + 1) / K; ay0, ay1 = y0 + py / K, y0 + (py + 1) / K
        reg = raw[int(ay0):max(int(ay0) + 1, int(np.ceil(ay1))), int(ax0):max(int(ax0) + 1, int(np.ceil(ax1)))]
        im[py, px, :3] = reg.reshape(-1, 3).mean(0); im[py, px, 3] = 255
    Image.fromarray(np.clip(im, 0, 255).astype(np.uint8), 'RGBA').save(path_out); return len(ys)
pm = json.load(open(os.path.join(SRC, '部件A3/parts.json')))['parts']
J = dict(torso=(370, 960), head=(450, 405), hand=(310, 880), foot=(332, 1378))
for nm, raw in (('torso', RT), ('head', RA), ('hand', RA), ('foot', RA)):
    pv = pm[nm]['pivot']; x0 = J[nm][0] - pv[0] * FPU; y0 = J[nm][1] - pv[1] * FPU
    n = restore(os.path.join(SRC, '部件A3', nm + '.png'), os.path.join('部件A4', nm + '.png'), raw, x0, y0); print(nm, '恢复', n)
for nm in ('hand', 'foot'):
    Image.open(os.path.join('部件A4', nm + '.png')).save(os.path.join('部件A4', nm + '_far.png'))
# 袖子条带：build_strips 里 ax0/ay0 = 袖子遮罩外框 - 6
sleeve = (arm.astype(bool)) & ~hand; ys_, xs_ = np.nonzero(sleeve); ax0, ay0 = xs_.min() - 6, ys_.min() - 6
os.system('python3 build_strips.py > /dev/null')                            # 先重新生成干净的条带（之前被补成平均色了）
print('sleeve 恢复', restore('条带/sleeve.png', '条带/sleeve.png', RA, ax0, ay0))
