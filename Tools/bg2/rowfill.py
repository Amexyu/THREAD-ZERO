"""补缝兜底：tile.py fit 之后竖条里还剩绿色的行，整行竖条（含羽化区）改成左右两侧像素之间的线性插值，
适合天空、水面这类横向变化小的区域；和 Gemini 补好的行之间上下羽化。
用法: rowfill.py in.png out.png --json tile_in.json"""
import sys, json, numpy as np, cv2
from PIL import Image
def run(src, dst, js):
    j = json.load(open(js)); x0, bw = j['x0'], j['bw']; f = max(12, bw // 6)
    im = Image.open(src); mode = im.mode
    a = np.asarray(im.convert('RGBA')).astype(np.float32); H, W = a.shape[:2]
    a = np.roll(a, W // 2, axis=1)                    # 竖条挪回正中间
    L, R = max(0, x0 - f - 2), min(W - 1, x0 + bw + f + 1)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    green = (g > 200) & (r < 80) & (b < 80)   # 只认纯绿（原来的阈值会把绿色山坡也当成没补的）
    bad = green[:, L:R].mean(1) > .002                 # 这一行竖条里还有绿
    bad = cv2.dilate(bad.astype(np.uint8)[:, None], np.ones((7, 1), np.uint8))[:, 0].astype(bool)
    n = R - L - 1; t = (np.arange(n) + 1) / (n + 1)
    fill = a.copy()
    fill[:, L + 1:R] = a[:, L:L + 1] * (1 - t[None, :, None]) + a[:, R:R + 1] * t[None, :, None]
    fill = cv2.GaussianBlur(fill, (0, 0), 2.5)
    mv = cv2.GaussianBlur(bad.astype(np.float32).reshape(-1, 1), (1, 0), 6).reshape(-1)
    m = np.repeat(mv[:, None], W, axis=1)[..., None].astype(np.float32)
    m[:, :L + 1] = 0; m[:, R:] = 0
    out = a * (1 - m) + fill * m
    out = np.roll(out, -(W // 2), axis=1)
    o = np.clip(out, 0, 255).astype(np.uint8)
    Image.fromarray(o if mode == 'RGBA' else o[..., :3], 'RGBA' if mode == 'RGBA' else 'RGB').save(dst)
    print('插值补了', int(bad.sum()), '行')
if __name__ == '__main__':
    a = sys.argv[1:]; run(a[0], a[1], a[a.index('--json') + 1])
