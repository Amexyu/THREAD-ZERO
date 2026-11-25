# 五章一览第二版（卡33）：第 2–5 章换成「分层原图」重画的亮色背景（layer_plate.py 拼的底图，没有去掉角色留下的涂抹），
# 前景还是桥 w64 的实机截图；第一章和标题画面不变。输出到 Docs/截图_2026-11-25/。
import os, sys, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import ui_action as UA, tour_sheet_v2 as TS
O = os.path.join(HERE, '出图'); V = os.path.join(O, '第二版'); os.makedirs(os.path.join(V, 'tour'), exist_ok=True)
NAMES = {'A': 'A糖果街机', 'B': 'B晴天涂鸦', 'C': 'C柠檬汽水'}
for d in 'ABC':
    shutil.copy(os.path.join(O, 'act_%s.png' % d), V); shutil.copy(os.path.join(O, 'title_%s.jpg' % d), V)
for ch in (2, 3, 4, 5):
    for d in 'ABC':
        bg = os.path.join(O, 'bg_%d-1_%s_分层重画.jpg' % (ch, NAMES[d]))
        UA.compose(bg, os.path.join(O, '实机_前景_%d-1.png' % ch), os.path.join(V, 'tour', 'act_%d_%s.png' % (ch, d)), shadow=True)
D = os.path.join(HERE, '..', '..', 'Docs', '截图_2026-11-25')
for d in 'ABC': TS.per_direction(d, V, os.path.join(D, '样图_明亮配色_五章一览_%s_第二版.jpg' % NAMES[d]))
TS.compare(V, os.path.join(D, '样图_明亮配色_三个方向逐章对比_第二版.jpg'))
