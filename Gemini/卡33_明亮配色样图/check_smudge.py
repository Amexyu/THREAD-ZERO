# 查「去掉角色留下的涂抹」：抹掉区域里的清晰度 ÷ 周围一圈的清晰度。低于 0.5 基本就是糊斑，要重出。
# 用法：python3 check_smudge.py 出图/实机_黑底_5-1.png 出图/实机_白底_5-1.png 出图/bg_5-1_A糖果街机.jpg [更多背景…]
import sys, numpy as np, cv2
from PIL import Image
def mask(black, white):
    B = np.asarray(Image.open(black).convert('RGB')).astype(np.float32)
    W = np.asarray(Image.open(white).convert('RGB')).astype(np.float32)
    a = 1 - (W - B).mean(-1) / 255.0
    m = cv2.dilate((a > 0.03).astype(np.uint8), np.ones((9, 9), np.uint8))
    mm = np.zeros_like(m); mm[300:900, 300:1700] = m[300:900, 300:1700]   # 只看舞台中间（不算四角的界面字）
    return mm, cv2.dilate(mm, np.ones((61, 61), np.uint8)) - mm
if __name__ == '__main__':
    inner, ring = mask(sys.argv[1], sys.argv[2])
    for f in sys.argv[3:]:
        g = np.asarray(Image.open(f).convert('L').resize((1920, 1080))).astype(np.float32)
        lap = cv2.GaussianBlur(np.abs(cv2.Laplacian(g, cv2.CV_32F, ksize=3)), (0, 0), 6)
        r = lap[inner > 0].mean() / lap[ring > 0].mean()
        print('%.2f  %s%s' % (r, f, '   ← 可能有糊斑' if r < 0.5 else ''))
