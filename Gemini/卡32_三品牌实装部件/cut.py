# 用 key2 抠图再调 armor_sheet.cut。用法同 armor_sheet.py cut：python3 cut.py <部件> <出图> id1..id5 [--fit w,dy,h] [--adj i:dx,dy,s] [--preview f]
import sys, os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))   # 仓库根目录（本脚本在 Gemini/卡32_三品牌实装部件/）
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'bg'))
import armor_sheet as A
from key2 import key2
A.key = lambda a: key2(a)
a = sys.argv
rest = a[8:]; adj = {}; preview = None
while rest:
    if rest[0] == '--adj': i, v = rest[1].split(':'); adj[int(i)] = tuple(float(x) for x in v.split(',')); rest = rest[2:]
    elif rest[0] == '--preview': preview = rest[1]; rest = rest[2:]
    elif rest[0] == '--fit': v_ = [float(x) for x in rest[1].split(',')] + [0, 0]; A.FIT[a[1]] = (v_[0], v_[1], v_[2]); rest = rest[2:]
    else: rest = rest[1:]
A.cut(a[1], a[2], a[3:8], adj, preview)
