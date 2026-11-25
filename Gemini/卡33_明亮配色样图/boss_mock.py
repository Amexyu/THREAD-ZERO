# 首领战样图：x-7 首领房间（分层原图重画的亮色背景）+ 实机前景，原版首领原地换成回收机器人首领。
#   前景是桥 w67 在首领关开打 3 秒时截的（首领刚走过来，还没贴到主角身上）。
#   用法: python3 boss_mock.py <章 1~5> <实机前景(抠好的 RGBA)> <亮色背景> <cute 首领 PNG> <输出.jpg>
import sys, os, json, numpy as np, cv2
from PIL import Image
HERE = os.path.dirname(os.path.abspath(__file__))
for p in (HERE, os.path.dirname(HERE)):
    if os.path.exists(os.path.join(p, 'ui_action.py')): sys.path.insert(0, p)
import ui_action as UA
import wave_mock as WM
BOSS_ROW = (589, 126, 1331, 158)   # 首领名字和血量那一行字（1920×1080）
def build(ch, fg_path, bg, cute_path, dst):
    fg = Image.open(fg_path).convert('RGBA'); a = np.asarray(fg).copy()
    hp = UA.hp_number_rect(a.astype(np.float32))   # 换首领之前量主角血量数字的位置
    m = (a[..., 3] > 60).astype(np.uint8); n, lab, st, _ = cv2.connectedComponentsWithStats(m, 8)
    cand = [i for i in range(1, n) if st[i][0] > 950 and st[i][1] > 300 and st[i][4] > 30000]
    bi = max(cand, key=lambda i: st[i][4]); x, y, w, h = st[bi][:4]
    a[cv2.dilate((lab == bi).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0, 3] = 0
    # 第一章那张截图下面有一条一次性提示（机库开放了……），拿掉（主角脚底那几行留着）
    dark = (a[..., 3] > 200) & (a[..., :3].max(-1) < 40); ys, xs = np.where(dark[800:960, 400:1500])
    if len(xs) > 20000:
        x0, x1, y0, y1 = 400 + xs.min(), 400 + xs.max(), 800 + ys.min(), 800 + ys.max()
        clr = np.zeros(a.shape[:2], bool); clr[y0 - 2:y1 + 3, x0 - 18:x1 + 3] = True
        hero = np.zeros(a.shape[:2], bool); hero[:y0 + 8, 660:900] = True
        a[clr & ~hero, 3] = 0
    out = Image.fromarray(a, 'RGBA')
    name = WM.NAMES[str(ch)][5]
    s = WM.fit(Image.open(cute_path).convert('RGBA'), name)
    out.alpha_composite(s, (int(round(x + w / 2 - s.width / 2)), int(round(y + h - s.height))))
    tmp = dst + '.fg.png'; out.save(tmp)
    if BOSS_ROW not in UA.HUD_TEXT: UA.HUD_TEXT.append(BOSS_ROW); UA.PILLS.append(BOSS_ROW)
    UA.compose(bg, tmp, dst, shadow=True, hp=hp)
if __name__ == '__main__':
    build(*sys.argv[1:6])
