# 实机全图 + 黑底 / 白底 → 去掉角色、怪、界面字的「底图」（给 Gemini 当构图参考）。
import sys, numpy as np, cv2
from PIL import Image
def plate(full, black, white, dst):
    F = cv2.cvtColor(np.asarray(Image.open(full).convert('RGB')), cv2.COLOR_RGB2BGR)
    B = np.asarray(Image.open(black).convert('RGB')).astype(np.float32)
    W = np.asarray(Image.open(white).convert('RGB')).astype(np.float32)
    a = 1 - (W - B).mean(-1) / 255.0
    m = (a > 0.03).astype(np.uint8) * 255
    m = cv2.dilate(m, np.ones((9, 9), np.uint8))
    out = cv2.inpaint(F, m, 11, cv2.INPAINT_TELEA)
    Image.fromarray(cv2.cvtColor(out, cv2.COLOR_BGR2RGB)).save(dst); print(dst)
if __name__ == '__main__':
    plate(*sys.argv[1:5])
