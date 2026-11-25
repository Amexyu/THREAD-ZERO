# 明亮配色样图（行动页）：新背景 + 实机抠出来的前景（角色、怪、无人机、血条、伤害数字、界面字）。
# 亮底上白字发虚：关卡名、波次、角落提示这几块界面字换成深色字 + 白色描边（亮度反过来）。
import sys, numpy as np
from PIL import Image, ImageDraw, ImageFilter

HUD_TEXT = [(16, 24, 230, 160), (800, 24, 1120, 76), (1600, 1008, 1910, 1050)]   # 关卡名、波次、右下角提示（1920×1080）
PILLS = [(40, 30, 230, 160), (800, 24, 1120, 76), (1600, 1008, 1860, 1050)]        # 垫白底的范围（避开屏幕四角的折角线）

def hp_number_rect(fg):
    # 主角血条的青绿色填充 → 右上角的血量数字（221 / 221）
    r, g, b, a = fg[..., 0], fg[..., 1], fg[..., 2], fg[..., 3]
    teal = (a > 200) & (g > 150) & (b > 130) & (r < 120) & (np.abs(g - b) < 60)
    rows = np.where(teal.sum(1) > 80)[0]
    if len(rows) == 0: return None
    y0 = rows.min(); cols = np.where(teal[y0:rows.max() + 1].sum(0) > 0)[0]
    x1 = cols.max()
    return (int(x1 - 60), int(y0 - 36), int(x1 + 28), int(y0 - 10))

def compose(bg_path, fg_path, dst, ink=(30, 34, 70), shadow=False, hp=None):
    bg = Image.open(bg_path).convert('RGB').resize((1920, 1080), Image.LANCZOS)
    fg = np.asarray(Image.open(fg_path).convert('RGBA')).astype(np.float32)
    rects = list(HUD_TEXT); hp = hp or hp_number_rect(fg)   # 首领战：薄荷色首领会被误认成血条，先在原前景上量好再传进来
    if hp: rects.append(hp)
    for x0, y0, x1, y1 in rects:
        sub = fg[y0:y1, x0:x1]; rgb = sub[..., :3]; L = rgb.mean(-1, keepdims=True) / 255
        sat = (rgb.max(-1, keepdims=True) - rgb.min(-1, keepdims=True)) / 255
        grey = sat < 0.18                                  # 只动黑白灰的字，橙色进度块不动
        inv = np.asarray(ink, np.float32) * L + 255 * (1 - L)   # 白 → 深色，深描边 → 白
        sub[..., :3] = np.where(grey, inv, rgb)
        sub[..., 3] = np.where(grey[..., 0] & (sub[..., 3] > 0), np.minimum(255, sub[..., 3] * 1.35), sub[..., 3])
    out = bg.convert('RGBA')
    # 亮底上的界面字：底下垫一块半透明白色圆角底，背景再花也读得清
    pad = Image.new('RGBA', out.size, (0, 0, 0, 0)); d = ImageDraw.Draw(pad)
    for x0, y0, x1, y1 in PILLS:
        al = fg[y0:y1, x0:x1, 3] > 60
        ys, xs = np.where(al)
        if len(xs) == 0: continue
        d.rounded_rectangle((x0 + xs.min() - 12, y0 + ys.min() - 8, x0 + xs.max() + 12, y0 + ys.max() + 8), 14, fill=(255, 255, 255, 200))
    out.alpha_composite(pad)
    if shadow:   # 亮底上的白色伤害数字、技能字：整层前景垫一圈淡淡的深色描影（界面字那几块不垫）
        al = Image.fromarray(fg[..., 3].astype(np.uint8)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(2.2))
        sa = np.asarray(al).astype(np.float32) * 0.6
        for x0, y0, x1, y1 in rects: sa[y0:y1, x0:x1] = 0
        sh = np.zeros_like(fg); sh[..., :3] = ink; sh[..., 3] = sa
        out.alpha_composite(Image.fromarray(sh.astype(np.uint8), 'RGBA'))
    out.alpha_composite(Image.fromarray(np.clip(fg, 0, 255).astype(np.uint8), 'RGBA'))
    out.convert('RGB').save(dst); print(dst)

if __name__ == '__main__':
    compose(sys.argv[1], sys.argv[2], sys.argv[3], shadow='--shadow' in sys.argv)
