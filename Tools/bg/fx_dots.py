"""找图层里的小色点（指示灯、天线顶的红灯、机柜的青色状态灯、暖色窗户），打印比例坐标，直接抄进 *_fx.json。

用法：python3 fx_dots.py <层.png> [red|cyan|warm|orange] [--min 12] [--max 4000] [--top 20]

- 只看不透明的像素（中景 / 地面层的品红底已经抠成透明）。
- 同色相连的一块算一个点；太小（噪点）或太大（整面墙、镜框）的块会被滤掉，用 --min / --max 调。
- 输出：像素框、u（横向比例）、v（竖向比例）、宽高。blinks 的 r 大约取 max(宽, 高) / 2 / 图片高度。
"""
import sys
from collections import deque

import numpy as np
from PIL import Image

PRESETS = {
    "red": lambda r, g, b: (r > 190) & (g < 110) & (b < 110),
    "cyan": lambda r, g, b: (g > 170) & (b > 150) & (r < 140),
    "warm": lambda r, g, b: (r > 220) & (g > 150) & (b < 150),
    "orange": lambda r, g, b: (r > 200) & (g > 90) & (g < 200) & (b < 110),
}


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    path = argv[1]
    kind = argv[2] if len(argv) > 2 and not argv[2].startswith("--") else "warm"
    opts = {"--min": 12, "--max": 4000, "--top": 20}
    for i, a in enumerate(argv):
        if a in opts and i + 1 < len(argv):
            opts[a] = int(argv[i + 1])
    if kind not in PRESETS:
        print("颜色只能是：" + " / ".join(PRESETS))
        return 1

    im = np.array(Image.open(path).convert("RGBA")).astype(int)
    h, w = im.shape[:2]
    r, g, b, a = im[..., 0], im[..., 1], im[..., 2], im[..., 3]
    mask = (a > 200) & PRESETS[kind](r, g, b)
    seen = np.zeros(mask.shape, dtype=bool)
    found = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        queue = deque([(y0, x0)])
        seen[y0, x0] = True
        xs, ys = [], []
        while queue:
            y, x = queue.popleft()
            xs.append(x)
            ys.append(y)
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    queue.append((ny, nx))
        if opts["--min"] <= len(xs) <= opts["--max"]:
            found.append((len(xs), min(xs), max(xs), min(ys), max(ys)))

    found.sort(key=lambda c: -c[0])
    print(f"{path}  {w}x{h}  颜色 {kind}  找到 {len(found)} 个点（列前 {opts['--top']} 个）")
    for n, x0, x1, y0, y1 in found[: opts["--top"]]:
        u, v = (x0 + x1) / 2 / w, (y0 + y1) / 2 / h
        rr = max(x1 - x0 + 1, y1 - y0 + 1) / 2 / h
        print(f"  u={u:.4f} v={v:.4f}  r≈{rr:.4f}  框=({x0},{y0})-({x1},{y1})  {x1 - x0 + 1}x{y1 - y0 + 1}  {n}px")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
